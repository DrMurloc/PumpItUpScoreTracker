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

        // Tier-lists overhaul C1 (design doc §6): materialized per-user relative tier
        // lists. The (MixId, ChartId) covering index serves the similar-players
        // aggregation — every user's category for one folder's charts in a single seek.
        modelBuilder.Entity<UserTierListEntryEntity>().ToTable("UserTierListEntry")
            .HasKey(e => new { e.MixId, e.UserId, e.ChartId });
        // Default 1.0 = full voice, so pre-backfill rows behave exactly as before the
        // freshness column existed until the Backfill User Tier Lists run re-stamps them.
        modelBuilder.Entity<UserTierListEntryEntity>()
            .Property(e => e.Freshness)
            .HasDefaultValue(1.0);
        modelBuilder.Entity<UserTierListEntryEntity>()
            .HasIndex(e => new { e.MixId, e.ChartId })
            .IncludeProperties(e => new { e.Category, e.Order, e.Freshness });
        modelBuilder.Entity<UserTierListEntryEntity>()
            .HasOne<UserEntity>()
            .WithMany()
            .HasForeignKey(e => e.UserId);
        modelBuilder.Entity<UserTierListEntryEntity>()
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);

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

        modelBuilder.Entity<ChartScoringLevelEntity>().ToTable("ChartScoringLevel");
        modelBuilder.Entity<UserPreferenceRatingEntity>().ToTable("UserPreferenceRating");
        modelBuilder.Entity<ChartPreferenceRatingEntity>().ToTable("ChartPreferenceRating");
    }
}
