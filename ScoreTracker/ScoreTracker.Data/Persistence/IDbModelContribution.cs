using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence;

/// <summary>
///     Lets a vertical assembly register its own entities with the single
///     <see cref="ChartAttemptDbContext" /> without the context referencing the vertical
///     (ADR-001 D4: one context, entities owned by their vertical). Implementations are
///     registered in DI by the vertical's AddXxx() wiring extension and applied at the end
///     of OnModelCreating; the design-time factory in CompositionRoot must list every
///     vertical's contribution or migrations will silently drop that vertical's tables.
/// </summary>
public interface IDbModelContribution
{
    void Contribute(ModelBuilder modelBuilder);
}
