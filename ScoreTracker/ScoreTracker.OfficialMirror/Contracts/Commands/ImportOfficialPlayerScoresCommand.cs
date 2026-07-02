using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record ImportOfficialPlayerScoresCommand
    (string Username, string Password, string Id, string ExpectedGameTag, bool IncludeBroken,
        bool SyncPiuTracker) : IRequest
    {
    }
}
