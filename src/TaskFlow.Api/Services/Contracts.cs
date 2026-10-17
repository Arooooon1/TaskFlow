using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Services;

public sealed record WorkInput(string Title, string Description, Priority Priority, int? AssigneeId, DateOnly? DueDate);
public sealed record UpdateWork(string Title, string Description, Priority Priority, int? AssigneeId, DateOnly? DueDate, Guid Version);
public sealed record StatusInput(WorkStatus Status, Guid Version);
public sealed record ItemDto(int Id, string Title, string Description, WorkStatus Status, Priority Priority,
    int? AssigneeId, string? AssigneeName, string? Initials, DateOnly? DueDate, DateTime CreatedAt, DateTime UpdatedAt, Guid Version, bool Overdue);
public sealed record PageDto<T>(List<T> Items, int Total, int Page, int PageSize, int TotalPages);
public sealed record HistoryDto(int Id, string Action, DateTime At);
public sealed record ReportDto(int Total, int New, int InProgress, int Done, int Overdue, int HighPriority, DateOnly Today);

public interface IWorkService
{
    Task<PageDto<ItemDto>> ListAsync(string? search, WorkStatus? status, Priority? priority, int? assigneeId, bool overdue, string sort, int page, int pageSize, CancellationToken ct);
    Task<ItemDto?> GetAsync(int id, CancellationToken ct);
    Task<ItemDto> CreateAsync(WorkInput input, CancellationToken ct);
    Task<ItemDto?> UpdateAsync(int id, UpdateWork input, CancellationToken ct);
    Task<ItemDto?> ChangeStatusAsync(int id, StatusInput input, CancellationToken ct);
    Task<ReportDto> ReportAsync(CancellationToken ct);
}
