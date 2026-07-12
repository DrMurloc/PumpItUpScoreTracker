using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTournamentRandomSettingsQuery(Guid TournamentId)
        : IQuery<IEnumerable<SavedRandomizerSettings>>
    {
    }
}
