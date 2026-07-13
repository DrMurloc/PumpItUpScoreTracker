using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetRandomSettingsQuery : IQuery<IEnumerable<SavedRandomizerSettings>>
    {
    }
}
