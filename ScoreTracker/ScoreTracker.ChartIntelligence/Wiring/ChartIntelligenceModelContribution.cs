using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;

namespace ScoreTracker.ChartIntelligence.Wiring;

/// <summary>
///     Registers the Chart Intelligence entities with the single
///     <see cref="ChartAttemptDbContext" /> (ADR-001 D4). Table names are pinned because
///     they used to come from the context's deleted DbSet property names; FK relations and
///     the ChartDifficultyRating composite key are reproduced verbatim from the context's
///     former OnModelCreating blocks.
/// </summary>
public sealed class ChartIntelligenceModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoOpRatingEntity>().ToTable("CoOpRating")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(c => c.ChartId);

        modelBuilder.Entity<UserCoOpRatingEntity>().ToTable("UserCoOpRating")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(c => c.ChartId);

        modelBuilder.Entity<UserCoOpRatingEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(c => c.UserId);

        modelBuilder.Entity<UserChartDifficultyRatingEntity>().ToTable("UserChartDifficultyRating")
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(ucdr => ucdr.UserId);

        modelBuilder.Entity<UserChartDifficultyRatingEntity>()
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(ucdr => ucdr.ChartId);

        modelBuilder.Entity<ChartDifficultyRatingEntity>().ToTable("ChartDifficultyRating")
            .HasKey(cdr => new { cdr.ChartId, cdr.MixId });

        modelBuilder.Entity<TierListEntryEntity>().ToTable("TierListEntry");


        // Population score variance per chart, refreshed by the daily scores rebuild.
        modelBuilder.Entity<ChartScoreStatsEntity>().ToTable("ChartScoreStats")
            .HasKey(e => new { e.MixId, e.ChartId });
        modelBuilder.Entity<ChartScoreStatsEntity>()
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);

        // Folder pass-count histograms per competitive-level bucket (round 7), refreshed
        // by the daily scores rebuild; read as a tiny keyed range per folder view.
        modelBuilder.Entity<FolderCohortStatsEntity>().ToTable("FolderCohortStats")
            .HasKey(e => new { e.MixId, e.ChartType, e.Level, e.Bucket });

        // The PUMBILITY tier lists (docs/design/pumbility-tier-list.md): per peer key, how many
        // players hold each of a folder's charts in their top-50 pool. The key prefix IS the
        // read — a folder for one peer key is a single seek, which is the only shape the page
        // and the nightly rewrite ask for.
        modelBuilder.Entity<PumbilityTierListEntryEntity>().ToTable("PumbilityTierListEntry")
            .HasKey(e => new { e.MixId, e.ChartType, e.Level, e.PeerKey, e.ChartId });

        // Where PUMBILITY comes from per band of the total (docs/design/pumbility-calculator.md D9):
        // a handful of rows per mix, rewritten wholesale by the same nightly sweep, read as one
        // keyed range by the calculator page.
        modelBuilder.Entity<PumbilityPoolCompositionEntity>().ToTable("PumbilityPoolComposition")
            .HasKey(e => new { e.MixId, e.BandKey });

        modelBuilder.Entity<ChartScoringLevelEntity>().ToTable("ChartScoringLevel");
        modelBuilder.Entity<UserPreferenceRatingEntity>().ToTable("UserPreferenceRating");
        modelBuilder.Entity<ChartPreferenceRatingEntity>().ToTable("ChartPreferenceRating");

        // The similarity graph's edges (chart-details overhaul B1): top-K per (mix, chart),
        // rebuilt wholesale by the nightly job. The PK prefix serves the page's only read
        // (one chart's neighbors); K ≤ 8 makes ordering in memory free. Two FKs onto Chart
        // can't both cascade (SQL Server multiple-cascade-paths), so the neighbor side
        // restricts — charts are never deleted in practice, and the nightly rebuild would
        // drop orphaned edges anyway.
        modelBuilder.Entity<ChartSimilarityEntity>().ToTable("ChartSimilarity")
            .HasKey(e => new { e.MixId, e.ChartId, e.SimilarChartId });
        modelBuilder.Entity<ChartSimilarityEntity>()
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);
        modelBuilder.Entity<ChartSimilarityEntity>()
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.SimilarChartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
