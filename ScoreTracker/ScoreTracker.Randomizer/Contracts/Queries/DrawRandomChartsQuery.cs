using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    /// <summary>
    ///     A published mirror of the transitional <c>GetRandomChartsQuery</c> (which lives in
    ///     Application): it lets another vertical request a weighted draw without referencing
    ///     Application. The Randomizer saga delegates to the same draw logic.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record DrawRandomChartsQuery(RandomSettings Settings, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IEnumerable<Chart>>;
}
