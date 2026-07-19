using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Communities.Wiring;

/// <summary>
///     Registers the Community entities with the single <see cref="ChartAttemptDbContext" />
///     (ADR-001 D4). Table names are pinned because they used to come from the context's
///     deleted DbSet property names; all four entities are attribute-configured.
/// </summary>
public sealed class CommunitiesModelContribution : IDbModelContribution
{
    public void Contribute(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommunityEntity>().ToTable("Community");
        modelBuilder.Entity<CommunityChannelEntity>().ToTable("CommunityChannel");
        modelBuilder.Entity<CommunityInviteCodeEntity>().ToTable("CommunityInviteCode");
        modelBuilder.Entity<CommunityMembershipEntity>().ToTable("CommunityMembership");
        modelBuilder.Entity<CommunityHighlightEntity>().ToTable("CommunityHighlight");
        modelBuilder.Entity<DiscordFeedSubscriptionEntity>().ToTable("DiscordFeedSubscription");
    }
}
