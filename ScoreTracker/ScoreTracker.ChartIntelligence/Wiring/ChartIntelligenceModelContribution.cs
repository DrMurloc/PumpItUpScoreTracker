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
        modelBuilder.Entity<ChartScoringLevelEntity>().ToTable("ChartScoringLevel");
        modelBuilder.Entity<UserPreferenceRatingEntity>().ToTable("UserPreferenceRating");
        modelBuilder.Entity<ChartPreferenceRatingEntity>().ToTable("ChartPreferenceRating");
    }
}
