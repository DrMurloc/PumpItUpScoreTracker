using MediatR;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record DeleteTournamentRandomSettingsCommand(Guid TournamentId, Name SettingsName) : IRequest
    {
    }
}
