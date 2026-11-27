using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nights.Models.Entities;

namespace Nights.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Memory> Memories => Set<Memory>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTutorial> ProjectTutorials => Set<ProjectTutorial>();
    public DbSet<ActiveDemoSession> ActiveDemoSessions => Set<ActiveDemoSession>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
    public DbSet<SystemAuditLog> SystemAuditLogs => Set<SystemAuditLog>();
    public DbSet<GameScore> GameScores => Set<GameScore>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // EF Core Global Query Filters (Soft-Delete / Çöp Kutusu İzolasyonu)
        builder.Entity<Memory>().HasQueryFilter(m => !m.IsDeleted);
        builder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Note>().HasQueryFilter(n => !n.IsDeleted);
        builder.Entity<GameScore>().HasQueryFilter(g => !g.IsDeleted);

        // İlişkili alt tablolar için eşleşen filtreler (EF Core 10622 uyarısını önler)
        builder.Entity<ProjectTutorial>().HasQueryFilter(t => t.Project != null && !t.Project.IsDeleted);
        builder.Entity<ActiveDemoSession>().HasQueryFilter(s => s.Project != null && !s.Project.IsDeleted);
    }
}
