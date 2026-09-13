using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Seasons.Infrastructure.Entities;

namespace ScoreTracker.Seasons.Wiring;

/// <summary>
///     Registers the Seasons vertical's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4): the season row itself (docs/design/seasons.md §6.1). Listed in
///     <c>VerticalModelContributions.All()</c>, which feeds the design-time factory and the
///     integration fixture.
/// </summary>
public sealed class SeasonsModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        // The key is the calendar number the whole schema uses as SeasonId (20264 = Fall 2026) —
        // never generated: the roll computes it from the date.
        modelBuilder.Entity<SeasonEntity>().ToTable("Season").HasKey(e => e.Id);
        modelBuilder.Entity<SeasonEntity>().Property(e => e.Id).ValueGeneratedNever();
        modelBuilder.Entity<SeasonEntity>().Property(e => e.Name).HasMaxLength(100);
    }
}
