using MediatR;

namespace ScoreTracker.Application.Commands
{
    [ExcludeFromCodeCoverage]
    public sealed record UpdateSongImagesCommand(bool IncludeSongsAlreadyWithImages = false) : IRequest
    {
    }
}
