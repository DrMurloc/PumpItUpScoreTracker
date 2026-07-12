using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Randomizer.Infrastructure.Entities;

namespace ScoreTracker.Randomizer.Wiring;

/// <summary>
///     Registers the Randomizer's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). UserRandomSettings moved here from the Catalog contribution with the
///     vertical extraction — table mapping unchanged, CLR namespace only.
/// </summary>
public sealed class RandomizerModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRandomSettingsEntity>().ToTable("UserRandomSettings");
    }
}
