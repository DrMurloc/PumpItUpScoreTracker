using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Ucs.Infrastructure.Entities;

namespace ScoreTracker.Ucs.Wiring;

/// <summary>
///     Registers the UCS vertical's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned explicitly because they used to come from the
///     context's deleted DbSet property names.
/// </summary>
public sealed class UcsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UcsChartEntity>().ToTable("UcsChart");
        modelBuilder.Entity<UcsChartLeaderboardEntryEntity>().ToTable("UcsChartLeaderboardEntry");
        modelBuilder.Entity<UcsChartTagEntity>().ToTable("UcsChartTag");
    }
}
