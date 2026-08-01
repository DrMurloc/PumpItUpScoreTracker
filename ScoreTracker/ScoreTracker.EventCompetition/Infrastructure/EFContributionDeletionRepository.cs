using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure.Entities;

namespace ScoreTracker.EventCompetition.Infrastructure;

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
        // Photos before the sessions they prove, roles last — a role is the only one another
        // organiser might still be looking at.
        if (!items.HasFlag(ContributionDeletionItems.TournamentResults)) return Task.CompletedTask;
        return UserDataPurge.DeleteAll(_factory, new[]
        {
            typeof(PhotoVerificationEntity),
            typeof(UserTournamentSessionEntity),
            typeof(UserQualifierEntity),
            typeof(UserTournamentRegistrationEntity),
            typeof(TournamentRoleEntity)
        }, userId, cancellationToken);
    }
}
