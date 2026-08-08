using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Wiring;

/// <summary>
///     Registers the chart-comment entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Listed in <c>VerticalModelContributions.All()</c>, without which scaffolded
///     migrations silently drop every table below.
/// </summary>
public sealed class ChartCommentsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        // Tables arrive with the persistence commit; the contribution is registered from the
        // vertical's first commit so the wiring is never the thing that is missing.
    }
}
