using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Data;

public sealed class TaskFlowDb(DbContextOptions<TaskFlowDb> options) : DbContext(options)
{
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<WorkHistory> WorkHistory => Set<WorkHistory>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<WorkItem>(b =>
        {
            b.Property(x => x.Title).HasMaxLength(120);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasIndex(x => new { x.Status, x.DueDate });
            b.HasIndex(x => x.AssigneeId);
            b.HasOne(x => x.Assignee).WithMany().OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.History).WithOne(x => x.WorkItem).HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TeamMember>().Property(x => x.Name).HasMaxLength(100);
        model.Entity<WorkHistory>().Property(x => x.Action).HasMaxLength(500);
    }
}
