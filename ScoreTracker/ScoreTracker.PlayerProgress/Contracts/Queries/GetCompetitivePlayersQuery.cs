using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The competitive band (±0.5) of <paramref name="Subject" /> — the viewer when null. A host
    ///     that shows another player's scores names them, so the board a peer line opens is the
    ///     band that line counted (docs/design/peers-abstraction.md D31).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetCompetitivePlayersQuery(ChartType ChartType, MixEnum Mix = MixEnum.Phoenix,
        Guid? Subject = null)
        : IQuery<IEnumerable<Guid>>
    {
    }
}
