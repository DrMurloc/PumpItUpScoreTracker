using Microsoft.EntityFrameworkCore;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Wiring;

/// <summary>
///     Registers the Catalog's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). The table name is pinned because it used to come from the context's
///     deleted DbSet property name.
/// </summary>
public sealed class CatalogModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRandomSettingsEntity>().ToTable("UserRandomSettings");
    }
}
