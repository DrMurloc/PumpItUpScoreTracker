using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Catalog.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRandomSettingsQuery : IQuery<IEnumerable<SavedRandomizerSettings>>
    {
    }
}
