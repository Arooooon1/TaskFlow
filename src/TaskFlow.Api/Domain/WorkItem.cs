namespace TaskFlow.Api.Domain;

public enum WorkStatus { New, InProgress, Done }
public enum Priority { Low, Medium, High }

public sealed class TeamMember
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Initials { get; set; } = "";
}

public sealed class WorkItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public WorkStatus Status { get; set; }
    public Priority Priority { get; set; }
    public int? AssigneeId { get; set; }
    public TeamMember? Assignee { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Application-managed optimistic concurrency works on SQLite and PostgreSQL.
    public Guid Version { get; set; } = Guid.NewGuid();
    public List<WorkHistory> History { get; set; } = [];
}

public sealed class WorkHistory
{
    public int Id { get; set; }
    public int WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public string Action { get; set; } = "";
    public DateTime At { get; set; }
}

public sealed class RuleViolation(string message) : Exception(message);
public sealed class VersionConflict() : Exception("Заявка уже изменена. Обновите данные и повторите действие.");

public static class WorkRules
{
    public static void ValidateTransition(WorkStatus from, WorkStatus to, int? assigneeId)
    {
        if (!Enum.IsDefined(to)) throw new RuleViolation("Неизвестный статус.");
        if (from == to) return;
        if ((from, to) is not ((WorkStatus.New, WorkStatus.InProgress)
            or (WorkStatus.InProgress, WorkStatus.Done)
            or (WorkStatus.InProgress, WorkStatus.New)
            or (WorkStatus.Done, WorkStatus.InProgress)))
            throw new RuleViolation("Сначала возьмите заявку в работу. Завершённую заявку можно вернуть в работу.");
        if (to == WorkStatus.InProgress && assigneeId is null)
            throw new RuleViolation("Перед началом работы назначьте исполнителя.");
    }

    public static void ValidateFields(string title, string description, Priority priority, int? assigneeId, WorkStatus status)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length is < 3 or > 120)
            throw new RuleViolation("Название должно содержать от 3 до 120 символов.");
        if (description.Length > 4000) throw new RuleViolation("Описание не должно превышать 4000 символов.");
        if (!Enum.IsDefined(priority)) throw new RuleViolation("Неизвестный приоритет.");
        if (status != WorkStatus.New && assigneeId is null)
            throw new RuleViolation("У заявки в работе или завершённой заявки должен быть исполнитель.");
    }

    public static bool IsOverdue(WorkItem item, DateOnly today) =>
        item.Status != WorkStatus.Done && item.DueDate is { } date && date < today;
}
