using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetRandomSettingsQuery : IRequest<IEnumerable<SavedRandomizerSettings>>
    {
    }
}
