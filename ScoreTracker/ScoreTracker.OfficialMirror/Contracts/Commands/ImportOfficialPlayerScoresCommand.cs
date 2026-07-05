using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record ImportOfficialPlayerScoresCommand
    (string Username, RedactedString Password, string Id, string ExpectedGameTag, bool IncludeBroken,
        bool SyncPiuTracker, MixEnum Mix = MixEnum.Phoenix) : IRequest
    {
    }
}
