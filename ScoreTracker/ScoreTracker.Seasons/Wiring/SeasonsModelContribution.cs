using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Seasons.Wiring;

/// <summary>
///     Registers the Seasons vertical's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Listed in <c>VerticalModelContributions.All()</c> from the vertical's first
///     commit so the design-time factory and the integration fixture carry it; the Season table
///     arrives with slice 1a's migration.
/// </summary>
public sealed class SeasonsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
    }
}
