using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Rivals.Wiring;

/// <summary>
///     Registers the Rivals entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Must be listed in <c>VerticalModelContributions.All()</c> or scaffolded
///     migrations silently drop every table below.
/// </summary>
public sealed class RivalsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
    }
}
