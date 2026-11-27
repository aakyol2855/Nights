using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nights.Models.Entities;

namespace Nights.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Slug).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(300).IsRequired();
        builder.Property(e => e.LiveDemoUrl).HasMaxLength(500);
        builder.Property(e => e.GitHubUrl).HasMaxLength(500);
        builder.Property(e => e.BadgeText).HasMaxLength(50);
        builder.Property(e => e.TechStack).HasMaxLength(100);
        builder.Property(e => e.DockerImageName).HasMaxLength(256);
        builder.Property(e => e.EnvironmentVariables).HasMaxLength(4000);
        builder.HasIndex(e => new { e.IsActive, e.DisplayOrder });
        builder.HasIndex(e => e.Slug);

        builder.HasMany(e => e.Tutorials)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.DemoSessions)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ActiveDemoSessionConfiguration : IEntityTypeConfiguration<ActiveDemoSession>
{
    public void Configure(EntityTypeBuilder<ActiveDemoSession> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Slug).HasMaxLength(120).IsRequired();
        builder.Property(e => e.ContainerId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ContainerName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.ImageName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.StoppedReason).HasMaxLength(100);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.Slug);
    }
}

public class ProjectTutorialConfiguration : IEntityTypeConfiguration<ProjectTutorial>
{
    public void Configure(EntityTypeBuilder<ProjectTutorial> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StepTitle).HasMaxLength(150).IsRequired();
        builder.Property(e => e.ContentMarkdown).IsRequired();
        builder.HasIndex(e => new { e.ProjectId, e.StepOrder });
    }
}
