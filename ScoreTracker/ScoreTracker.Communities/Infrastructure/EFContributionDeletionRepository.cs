using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Infrastructure;

internal sealed class EFContributionDeletionRepository : IContributionDeletionRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFContributionDeletionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public Task Delete(Guid userId, ContributionDeletionItems items,
        CancellationToken cancellationToken = default)
    {
        // Communities the player owns are untouched here: owning one blocks account deletion
        // outright, and leaving a community you run is a different act from deleting data.
        if (!items.HasFlag(ContributionDeletionItems.CommunityMemberships)) return Task.CompletedTask;
        return UserDataPurge.DeleteAll(_factory, new[]
        {
            typeof(CommunityHighlightEntity),
            typeof(CommunityMembershipEntity)
        }, userId, cancellationToken);
    }
}
