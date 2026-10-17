using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Services;

public sealed record WorkspaceDto(List<TeamMember> Team, ReportDto Summary);

// TPL / concurrent I/O: each operation owns a scope and therefore its own DbContext.
// A single EF DbContext must never be queried concurrently.
public sealed class WorkspaceService(IServiceScopeFactory scopes)
{
    public async Task<WorkspaceDto> LoadAsync(CancellationToken ct)
    {
        var team = LoadTeamAsync(ct);
        var report = LoadReportAsync(ct);
        await Task.WhenAll(team, report);
        return new(await team, await report);
    }
    private async Task<List<TeamMember>> LoadTeamAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TaskFlowDb>().TeamMembers.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct);
    }
    private async Task<ReportDto> LoadReportAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkService>().ReportAsync(ct);
    }
}
