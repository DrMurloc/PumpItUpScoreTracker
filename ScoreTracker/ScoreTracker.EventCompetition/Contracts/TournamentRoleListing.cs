using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts
{
    [ExcludeFromCodeCoverage]
    public sealed record TournamentRoleListing(Guid TournamentId, Name TournamentName, TournamentRole Role,
        bool IsUnlisted)
    {
    }
}
