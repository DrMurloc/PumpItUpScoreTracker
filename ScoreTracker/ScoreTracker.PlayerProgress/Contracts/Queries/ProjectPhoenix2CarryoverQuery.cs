using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     Prices a player's Phoenix 1 record under Phoenix 2's rules. <paramref name="Pool" />
    ///     null is the merged top-50; Phoenix 2 keeps Singles and Doubles as independent pools
    ///     as well, so all three are askable.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ProjectPhoenix2CarryoverQuery(Guid UserId, ChartType? Pool = null)
        : IQuery<Phoenix2CarryoverRecord>;
}
