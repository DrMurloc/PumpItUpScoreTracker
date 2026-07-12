using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record TournamentRoleInviteRecord(Guid Token, Guid TournamentId, TournamentRole Role,
        DateTimeOffset? ExpiresAt)
    {
    }
}
