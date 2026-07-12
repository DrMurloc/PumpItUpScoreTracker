using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.HomePage.Infrastructure.Entities;

namespace ScoreTracker.HomePage.Wiring;

/// <summary>
///     Registers the HomePage vertical's entities with the single
///     <see cref="ChartAttemptDbContext" /> (ADR-001 D4). Must be listed in
///     CompositionRoot.VerticalModelContributions.All() — omitting it silently drops
///     these tables from scaffolded migrations.
/// </summary>
public sealed class HomePageModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HomePageEntity>().ToTable("HomePage");
        modelBuilder.Entity<HomePageWidgetEntity>().ToTable("HomePageWidget");
    }
}
