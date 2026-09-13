using Microsoft.EntityFrameworkCore;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;

namespace ScoreTracker.Catalog.Wiring;

/// <summary>
///     Registers the Catalog's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned because they used to come from the context's
///     deleted DbSet property names; ChartVideo's FK relation is reproduced verbatim from
///     the context's former OnModelCreating block.
/// </summary>
public sealed class CatalogModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AvatarEntity>().ToTable("Avatar");
        modelBuilder.Entity<SongNameLanguageEntity>().ToTable("SongNameLanguage");
        modelBuilder.Entity<ExternalChartAliasEntity>().ToTable("ExternalChartAlias");
        // Still mapped, still read: the Chabala lens shows his archived hand tags, and it is
        // the only surface that does (docs/design/nuke-old-skill-categories.md §7).
        modelBuilder.Entity<ChartSkillArchiveEntity>().ToTable("ChartSkillArchive");
        modelBuilder.Entity<ChartSkillMetricEntity>().ToTable("ChartSkillMetric")
            .HasKey(e => new { e.ChartId, e.Source, e.MetricName });
        modelBuilder.Entity<ChartStepChartEntity>().ToTable("ChartStepChart");
        modelBuilder.Entity<ChartFolderBaselineEntity>().ToTable("ChartFolderBaseline")
            .HasKey(e => new { e.MixId, e.ChartType, e.Level, e.Badge });

        modelBuilder.Entity<ChartVideoEntity>().ToTable("ChartVideo")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);

        // A mix's patches, and the shared ChartMix row's link onto them — declared here, the way
        // ChartVideo's key onto Chart is, because the table is this vertical's while the column
        // sits on Data's entity (docs/design/chart-versions.md §2).
        modelBuilder.Entity<MixVersionEntity>().ToTable("MixVersion")
            .HasIndex(e => new { e.MixId, e.Name })
            .IsUnique();
        modelBuilder.Entity<MixVersionEntity>()
            .HasOne<MixEntity>()
            .WithMany()
            .HasForeignKey(e => e.MixId);
        modelBuilder.Entity<ChartMixEntity>()
            .HasOne<MixVersionEntity>()
            .WithMany()
            .HasForeignKey(e => e.AddedInVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
