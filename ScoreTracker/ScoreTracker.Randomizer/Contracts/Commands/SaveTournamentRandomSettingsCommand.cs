using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Tournament-scoped settings library (replaces the retired Match-subsystem
    ///     storage). Writes require Head TO or TO on the tournament; assistants read.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record SaveTournamentRandomSettingsCommand(Guid TournamentId, Name SettingsName,
        RandomSettings Settings, MixEnum Mix = MixEnum.Phoenix) : IRequest
    {
    }
}
