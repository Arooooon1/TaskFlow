using System.Text;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api;
using TaskFlow.Api.Data;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<TaskFlowDb>(options =>
{
    if (builder.Configuration["Database:Provider"] == "Postgres")
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Postgres."));
    else
    {
        var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? $"Data Source={Path.Combine(dataDirectory, "taskflow.db")}");
    }
});
builder.Services.AddScoped<IWorkService, WorkService>();
builder.Services.AddScoped<WorkspaceService>();
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "same-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
    await next(context);
});
if (app.Configuration.GetValue("Database:Initialize", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TaskFlowDb>();
    // SQLite migrations are the default; PostgreSQL demo setup uses the same EF model.
    if (db.Database.IsSqlite()) await db.Database.MigrateAsync();
    else await db.Database.EnsureCreatedAsync();
    if (app.Configuration.GetValue("Demo:Seed", true))
        await DemoSeed.RunAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>(), CancellationToken.None);
}
app.MapOpenApi();
app.MapGet("/api/workspace", async (WorkspaceService service, CancellationToken ct) => Results.Ok(await service.LoadAsync(ct)));
app.MapGet("/api/health", async (TaskFlowDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" }) : Results.StatusCode(503));
app.MapGet("/api/team", async (TaskFlowDb db, CancellationToken ct) =>
    Results.Ok(await db.TeamMembers.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)));
app.MapGet("/api/items", async (IWorkService service, CancellationToken ct, string? search = null,
    WorkStatus? status = null, Priority? priority = null, int? assigneeId = null, bool overdue = false,
    string sort = "newest", int page = 1, int pageSize = 12) =>
    Results.Ok(await service.ListAsync(search, status, priority, assigneeId, overdue, sort, page, pageSize, ct)));
app.MapGet("/api/items/{id:int}", async (int id, IWorkService service, CancellationToken ct) =>
    await service.GetAsync(id, ct) is { } item ? Results.Ok(item) : Results.NotFound());
app.MapPost("/api/items", async (WorkInput input, IWorkService service, CancellationToken ct) =>
{
    var item = await service.CreateAsync(input, ct);
    return Results.Created($"/api/items/{item.Id}", item);
});
app.MapPut("/api/items/{id:int}", async (int id, UpdateWork input, IWorkService service, CancellationToken ct) =>
    await service.UpdateAsync(id, input, ct) is { } item ? Results.Ok(item) : Results.NotFound());
app.MapPatch("/api/items/{id:int}/status", async (int id, StatusInput input, IWorkService service, CancellationToken ct) =>
    await service.ChangeStatusAsync(id, input, ct) is { } item ? Results.Ok(item) : Results.NotFound());
app.MapGet("/api/items/{id:int}/history", async (int id, TaskFlowDb db, CancellationToken ct) =>
{
    if (!await db.WorkItems.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
    return Results.Ok(await db.WorkHistory.AsNoTracking().Where(x => x.WorkItemId == id)
        .OrderByDescending(x => x.Id).Select(x => new HistoryDto(x.Id, x.Action, x.At)).ToListAsync(ct));
});
app.MapGet("/api/reports/summary", async (IWorkService service, CancellationToken ct) => Results.Ok(await service.ReportAsync(ct)));
app.MapGet("/api/reports/export", async (TaskFlowDb db, CancellationToken ct) =>
{
    var rows = await db.WorkItems.AsNoTracking().Include(x => x.Assignee).OrderBy(x => x.Id).ToListAsync(ct);
    static string Cell(string? value)
    {
        var text = value ?? "";
        if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
    var csv = new StringBuilder("Id,Title,Status,Priority,Assignee,DueDate\r\n");
    foreach (var x in rows) csv.AppendLine(string.Join(",", x.Id, Cell(x.Title), x.Status, x.Priority, Cell(x.Assignee?.Name), x.DueDate?.ToString("yyyy-MM-dd")));
    return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8", "taskflow-report.csv");
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
app.Run();
public partial class Program;
