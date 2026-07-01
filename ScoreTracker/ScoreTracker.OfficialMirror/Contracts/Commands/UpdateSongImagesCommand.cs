using MediatR;

namespace ScoreTracker.OfficialMirror.Contracts.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdateSongImagesCommand(bool IncludeSongsAlreadyWithImages = false) : IRequest
    {
    }
}
