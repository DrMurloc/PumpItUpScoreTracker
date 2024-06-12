using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Records
{
    public sealed record UserTournamentRole(Guid TournamentId, Guid UserId, TournamentRole Role)
    {
    }
}
