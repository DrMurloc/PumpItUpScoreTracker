using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;

namespace ScoreTracker.WeeklyChallenge.Wiring;

/// <summary>
///     Registers the Weekly Challenge entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned because they used to come from the context's
///     deleted DbSet property names; all four entities are attribute-configured.
/// </summary>
public sealed class WeeklyChallengeModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeeklyTournamentChartEntity>().ToTable("WeeklyTournamentChart");
        modelBuilder.Entity<WeeklyUserEntry>().ToTable("WeeklyUserEntry");
        modelBuilder.Entity<UserWeeklyPlacingEntity>().ToTable("UserWeeklyPlacing");
        modelBuilder.Entity<PastTourneyChartsEntity>().ToTable("PastTourneyCharts");

        // Daily Step (sibling feature, same bounded context): its own board + entries + history.
        modelBuilder.Entity<DailyStepChartEntity>().ToTable("DailyStepChart");
        modelBuilder.Entity<DailyStepEntryEntity>().ToTable("DailyStepEntry");
        modelBuilder.Entity<UserDailyStepPlacingEntity>().ToTable("UserDailyStepPlacing");
    }
}
