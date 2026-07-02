using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record UserTournamentRole(Guid TournamentId, Guid UserId, TournamentRole Role)
    {
    }
}
