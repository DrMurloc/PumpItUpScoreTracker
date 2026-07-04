using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record ProcessOfficialLeaderboardsCommand(MixEnum Mix = MixEnum.Phoenix) : IRequest
    {
    }
}
