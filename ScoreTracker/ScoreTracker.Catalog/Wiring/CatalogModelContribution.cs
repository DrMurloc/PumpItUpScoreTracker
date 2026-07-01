using Microsoft.EntityFrameworkCore;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;

namespace ScoreTracker.Catalog.Wiring;

/// <summary>
///     Registers the Catalog's entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned because they used to come from the context's
///     deleted DbSet property names; ChartVideo's FK relation is reproduced verbatim from
///     the context's former OnModelCreating block.
/// </summary>
public sealed class CatalogModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRandomSettingsEntity>().ToTable("UserRandomSettings");
        modelBuilder.Entity<ChartSkillEntity>().ToTable("ChartSkill");
        modelBuilder.Entity<SongNameLanguageEntity>().ToTable("SongNameLanguage");

        modelBuilder.Entity<ChartVideoEntity>().ToTable("ChartVideo")
            .HasOne<ChartEntity>()
            .WithMany()
            .HasForeignKey(e => e.ChartId);
    }
}
