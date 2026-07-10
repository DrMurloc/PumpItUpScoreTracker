using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;

namespace ScoreTracker.PlayerProgress.Wiring;

/// <summary>
///     Registers the Player Progress entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned because they used to come from the context's
///     deleted DbSet property names; the ParagonLevel default moved verbatim from the
///     context's OnModelCreating.
/// </summary>
public sealed class PlayerProgressModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerStatsEntity>().ToTable("PlayerStats");
        modelBuilder.Entity<PlayerHistoryEntity>().ToTable("PlayerHistory");
        modelBuilder.Entity<UserTitleEntity>().ToTable("UserTitle");
        modelBuilder.Entity<UserHighestTitleEntity>().ToTable("UserHighestTitle");
        modelBuilder.Entity<SuggestionFeedbackEntity>().ToTable("SuggestionFeedback");
        modelBuilder.Entity<ScoreHighlightEntity>().ToTable("ScoreHighlight");
        modelBuilder.Entity<PlayerMilestoneEntity>().ToTable("PlayerMilestone");
        modelBuilder.Entity<PlayerSeasonRecapEntity>().ToTable("PlayerSeasonRecap");
        modelBuilder.Entity<PlayerSeasonRecapEntity>().HasKey(e => new { e.UserId, e.MixId });

        // Session lookups (page deep-links, future import-results reads) skip the
        // pre-capture rows entirely.
        modelBuilder.Entity<ScoreHighlightEntity>().HasIndex(e => e.SessionId)
            .HasFilter("[SessionId] IS NOT NULL");

        modelBuilder.Entity<UserTitleEntity>().Property(e => e.ParagonLevel)
            .HasDefaultValue(ParagonLevel.None.ToString());
    }
}
