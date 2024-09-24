using MediatR;

namespace ScoreTracker.Application.Commands
{
    public sealed record UpdateSongImagesCommand(bool IncludeSongsAlreadyWithImages = false) : IRequest
    {
    }
}
