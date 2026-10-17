using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Api.Data;
using TaskFlow.Api.Domain;
using TaskFlow.Api.Services;
using Xunit;

namespace TaskFlow.Tests;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"taskflow-test-{Guid.NewGuid()}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={DatabasePath}");
        builder.UseSetting("Demo:Seed", "false");
        builder.UseSetting("AllowedHosts", "*");
    }
    public async Task<int> AddMember()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDb>();
        var member = new TeamMember { Name = "Test Developer", Initials = "TD", Role = "Backend" };
        db.TeamMembers.Add(member); await db.SaveChangesAsync(); return member.Id;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(DatabasePath + suffix)) File.Delete(DatabasePath + suffix);
    }
}

public sealed class ApiTests
{
    [Fact]
    public async Task Workspace_loads_concurrent_queries_in_separate_scopes()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        await app.AddMember(); await Create(client);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.GetFromJsonAsync<WorkspaceDto>("/api/workspace", Json)));
        Assert.All(results, result => { Assert.Single(result!.Team); Assert.Equal(1, result.Summary.Total); });
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static async Task<ItemDto> Create(HttpClient client, string title = "Review task", int? member = null, DateOnly? due = null)
    {
        var response = await client.PostAsJsonAsync("/api/items", new WorkInput(title, "Integration test", Priority.High, member, due), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<ItemDto>(Json))!;
        Assert.EndsWith($"/api/items/{item.Id}", response.Headers.Location!.ToString());
        return item;
    }

    [Fact]
    public async Task Complete_workflow_records_history_and_excludes_completed_from_overdue()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        var member = await app.AddMember();
        var item = await Create(client, member: member, due: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1));
        var started = await client.PatchAsJsonAsync($"/api/items/{item.Id}/status", new StatusInput(WorkStatus.InProgress, item.Version), Json);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        item = (await started.Content.ReadFromJsonAsync<ItemDto>(Json))!;
        var finished = await client.PatchAsJsonAsync($"/api/items/{item.Id}/status", new StatusInput(WorkStatus.Done, item.Version), Json);
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);
        var report = await client.GetFromJsonAsync<ReportDto>("/api/reports/summary", Json);
        Assert.Equal(1, report!.Done); Assert.Equal(0, report.Overdue);
        var events = await client.GetFromJsonAsync<List<HistoryDto>>($"/api/items/{item.Id}/history", Json);
        Assert.Equal(3, events!.Count);
    }

    [Fact]
    public async Task Invalid_transition_is_atomic()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        var item = await Create(client);
        var response = await client.PatchAsJsonAsync($"/api/items/{item.Id}/status", new StatusInput(WorkStatus.Done, item.Version), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var after = await client.GetFromJsonAsync<ItemDto>($"/api/items/{item.Id}", Json);
        Assert.Equal(WorkStatus.New, after!.Status); Assert.Equal(item.Version, after.Version);
        Assert.Single((await client.GetFromJsonAsync<List<HistoryDto>>($"/api/items/{item.Id}/history", Json))!);
    }

    [Fact]
    public async Task Stale_update_returns_conflict_and_keeps_first_update()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        var item = await Create(client);
        var input = new UpdateWork("First update", item.Description, Priority.Low, null, null, item.Version);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/items/{item.Id}", input, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/items/{item.Id}", input with { Title = "Lost update" }, Json)).StatusCode);
        var stored = await client.GetFromJsonAsync<ItemDto>($"/api/items/{item.Id}", Json);
        Assert.Equal("First update", stored!.Title);
    }

    [Fact]
    public async Task Search_filters_and_pagination_are_combined()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        await Create(client, "Alpha urgent"); await Create(client, "Beta task"); await Create(client, "Alpha follow up");
        var result = await client.GetFromJsonAsync<PageDto<ItemDto>>("/api/items?search=alpha&priority=High&pageSize=1&page=2", Json);
        Assert.Equal(2, result!.Total); Assert.Equal(2, result.TotalPages); Assert.Single(result.Items);
        Assert.Equal("Alpha urgent", result.Items[0].Title);
    }

    [Theory]
    [InlineData("/api/items?page=0")]
    [InlineData("/api/items?pageSize=101")]
    [InlineData("/api/items?sort=invalid")]
    [InlineData("/api/items?status=Unknown")]
    public async Task Invalid_query_is_rejected(string path)
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Unknown_assignee_and_empty_title_do_not_create_rows()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/items", new WorkInput("Valid", "", Priority.Low, 999, null), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/items", new WorkInput(" ", "", Priority.Low, null, null), Json)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<ReportDto>("/api/reports/summary", Json))!.Total);
    }

    [Fact]
    public async Task Csv_export_quotes_formulas_and_returns_attachment()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        await Create(client, "=HYPERLINK(\"example\")");
        var response = await client.GetAsync("/api/reports/export");
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition!.ToString());
        Assert.Contains("\"'=HYPERLINK(\"\"example\"\")\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_item_and_history_return_not_found()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/999/history")).StatusCode);
    }

    [Fact]
    public async Task Data_survives_a_new_service_scope_and_request()
    {
        using var app = new AppFactory(); using var client = app.CreateClient();
        var item = await Create(client);
        using var anotherClient = app.CreateClient();
        Assert.Equal(item.Title, (await anotherClient.GetFromJsonAsync<ItemDto>($"/api/items/{item.Id}", Json))!.Title);
        Assert.True(File.Exists(app.DatabasePath));
    }
}
