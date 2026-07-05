using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

[ExcludeFromCodeCoverage]
public sealed record StartLeaderboardImportCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
