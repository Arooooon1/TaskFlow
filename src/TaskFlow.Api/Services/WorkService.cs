using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Services;

public sealed class WorkService(TaskFlowDb db, TimeProvider clock) : IWorkService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private ItemDto Map(WorkItem x) => new(x.Id, x.Title, x.Description, x.Status, x.Priority, x.AssigneeId,
        x.Assignee?.Name, x.Assignee?.Initials, x.DueDate, x.CreatedAt, x.UpdatedAt, x.Version, WorkRules.IsOverdue(x, Today));

    public async Task<PageDto<ItemDto>> ListAsync(string? search, WorkStatus? status, Priority? priority,
        int? assigneeId, bool overdue, string sort, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new RuleViolation("Страница: от 1. Размер страницы: от 1 до 100.");
        if (status is not null && !Enum.IsDefined(status.Value)) throw new RuleViolation("Неизвестный статус.");
        if (priority is not null && !Enum.IsDefined(priority.Value)) throw new RuleViolation("Неизвестный приоритет.");
        if ((search?.Length ?? 0) > 200) throw new RuleViolation("Поисковый запрос слишком длинный.");
        var q = db.WorkItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLower();
            q = q.Where(x => x.Title.ToLower().Contains(text) || x.Description.ToLower().Contains(text));
        }
        if (status.HasValue) q = q.Where(x => x.Status == status);
        if (priority.HasValue) q = q.Where(x => x.Priority == priority);
        if (assigneeId.HasValue) q = q.Where(x => x.AssigneeId == assigneeId);
        var today = Today;
        if (overdue) q = q.Where(x => x.Status != WorkStatus.Done && x.DueDate != null && x.DueDate < today);
        var total = await q.CountAsync(ct);
        q = sort switch
        {
            "priority" => q.OrderByDescending(x => x.Priority).ThenByDescending(x => x.Id),
            "due" => q.OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate).ThenBy(x => x.Id),
            "newest" => q.OrderByDescending(x => x.Id),
            _ => throw new RuleViolation("Сортировка: newest, priority или due.")
        };
        // A stable order is essential: rows must not jump between pages.
        var skip = ((long)page - 1) * pageSize;
        var rows = skip > int.MaxValue ? [] : await q.Include(x => x.Assignee).Skip((int)skip).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<ItemDto?> GetAsync(int id, CancellationToken ct)
    {
        var item = await db.WorkItems.AsNoTracking().Include(x => x.Assignee).SingleOrDefaultAsync(x => x.Id == id, ct);
        return item is null ? null : Map(item);
    }

    private async Task ValidateAssignee(int? id, CancellationToken ct)
    {
        if (id.HasValue && !await db.TeamMembers.AnyAsync(x => x.Id == id, ct))
            throw new RuleViolation("Исполнитель не найден.");
    }

    public async Task<ItemDto> CreateAsync(WorkInput input, CancellationToken ct)
    {
        WorkRules.ValidateFields(input.Title, input.Description ?? "", input.Priority, input.AssigneeId, WorkStatus.New);
        await ValidateAssignee(input.AssigneeId, ct);
        var item = new WorkItem { Title = input.Title.Trim(), Description = (input.Description ?? "").Trim(), Priority = input.Priority,
            AssigneeId = input.AssigneeId, DueDate = input.DueDate, CreatedAt = Now, UpdatedAt = Now };
        item.History.Add(new() { Action = "Заявка создана", At = Now });
        db.WorkItems.Add(item);
        await db.SaveChangesAsync(ct);
        return (await GetAsync(item.Id, ct))!;
    }

    public async Task<ItemDto?> UpdateAsync(int id, UpdateWork input, CancellationToken ct)
    {
        var item = await db.WorkItems.FindAsync([id], ct);
        if (item is null) return null;
        if (item.Version != input.Version) throw new VersionConflict();
        WorkRules.ValidateFields(input.Title, input.Description ?? "", input.Priority, input.AssigneeId, item.Status);
        await ValidateAssignee(input.AssigneeId, ct);
        var changes = new List<string>();
        if (item.Title != input.Title.Trim()) changes.Add("название");
        if (item.Description != (input.Description ?? "").Trim()) changes.Add("описание");
        if (item.Priority != input.Priority) changes.Add("приоритет");
        if (item.AssigneeId != input.AssigneeId) changes.Add("исполнитель");
        if (item.DueDate != input.DueDate) changes.Add("срок");
        if (changes.Count == 0) return await GetAsync(id, ct);
        item.Title = input.Title.Trim(); item.Description = (input.Description ?? "").Trim();
        item.Priority = input.Priority; item.AssigneeId = input.AssigneeId; item.DueDate = input.DueDate;
        item.UpdatedAt = Now; item.Version = Guid.NewGuid();
        db.WorkHistory.Add(new() { WorkItemId = id, Action = "Изменено: " + string.Join(", ", changes), At = Now });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ItemDto?> ChangeStatusAsync(int id, StatusInput input, CancellationToken ct)
    {
        var item = await db.WorkItems.FindAsync([id], ct);
        if (item is null) return null;
        if (item.Version != input.Version) throw new VersionConflict();
        WorkRules.ValidateTransition(item.Status, input.Status, item.AssigneeId);
        if (item.Status == input.Status) return await GetAsync(id, ct);
        var old = item.Status;
        item.Status = input.Status; item.UpdatedAt = Now; item.Version = Guid.NewGuid();
        db.WorkHistory.Add(new() { WorkItemId = id, Action = $"Статус: {old} → {input.Status}", At = Now });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ReportDto> ReportAsync(CancellationToken ct)
    {
        var today = Today;
        // One SQL query, one context. Do not run parallel EF queries on a shared DbContext.
        var counts = await db.WorkItems.AsNoTracking().GroupBy(x => 1).Select(g => new
        {
            Total = g.Count(), New = g.Count(x => x.Status == WorkStatus.New),
            InProgress = g.Count(x => x.Status == WorkStatus.InProgress), Done = g.Count(x => x.Status == WorkStatus.Done),
            Overdue = g.Count(x => x.Status != WorkStatus.Done && x.DueDate != null && x.DueDate < today),
            High = g.Count(x => x.Status != WorkStatus.Done && x.Priority == Priority.High)
        }).SingleOrDefaultAsync(ct);
        return counts is null ? new(0, 0, 0, 0, 0, 0, today) :
            new(counts.Total, counts.New, counts.InProgress, counts.Done, counts.Overdue, counts.High, today);
    }
}
