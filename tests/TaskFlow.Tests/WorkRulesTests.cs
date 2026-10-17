using TaskFlow.Api.Domain;
using Xunit;

namespace TaskFlow.Tests;

public sealed class WorkRulesTests
{
    [Theory]
    [InlineData(WorkStatus.New, WorkStatus.InProgress)]
    [InlineData(WorkStatus.InProgress, WorkStatus.Done)]
    [InlineData(WorkStatus.Done, WorkStatus.InProgress)]
    [InlineData(WorkStatus.InProgress, WorkStatus.New)]
    public void Allowed_transitions_succeed_with_assignee(WorkStatus from, WorkStatus to) =>
        WorkRules.ValidateTransition(from, to, 1);

    [Theory]
    [InlineData(WorkStatus.New, WorkStatus.Done)]
    [InlineData(WorkStatus.Done, WorkStatus.New)]
    public void Skipping_work_is_rejected(WorkStatus from, WorkStatus to) =>
        Assert.Throws<RuleViolation>(() => WorkRules.ValidateTransition(from, to, 1));

    [Fact]
    public void Cannot_start_without_assignee() => Assert.Throws<RuleViolation>(() =>
        WorkRules.ValidateTransition(WorkStatus.New, WorkStatus.InProgress, null));

    [Theory]
    [InlineData(WorkStatus.InProgress)]
    [InlineData(WorkStatus.Done)]
    public void Cannot_unassign_started_work(WorkStatus status) => Assert.Throws<RuleViolation>(() =>
        WorkRules.ValidateFields("Valid title", "", Priority.Medium, null, status));

    [Theory]
    [InlineData(WorkStatus.New, -1, true)]
    [InlineData(WorkStatus.InProgress, -1, true)]
    [InlineData(WorkStatus.Done, -1, false)]
    [InlineData(WorkStatus.New, 0, false)]
    [InlineData(WorkStatus.New, 1, false)]
    public void Overdue_respects_status_and_day_boundary(WorkStatus status, int offset, bool expected)
    {
        var today = new DateOnly(2026, 10, 17);
        Assert.Equal(expected, WorkRules.IsOverdue(new() { Status = status, DueDate = today.AddDays(offset) }, today));
    }

    [Fact]
    public void Missing_deadline_is_not_overdue() => Assert.False(WorkRules.IsOverdue(new(), new(2026,10,17)));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Invalid_title_is_rejected(string title) => Assert.Throws<RuleViolation>(() =>
        WorkRules.ValidateFields(title, "", Priority.Low, null, WorkStatus.New));

    [Fact]
    public void Unknown_priority_is_rejected() => Assert.Throws<RuleViolation>(() =>
        WorkRules.ValidateFields("Valid title", "", (Priority)99, null, WorkStatus.New));
}
