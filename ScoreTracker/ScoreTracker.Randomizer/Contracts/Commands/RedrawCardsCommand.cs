using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Refills an existing draw (a match, or the personal draw) with a fresh card set
    ///     under the SAME slug — spectators keep following it. Protect/veto states reset
    ///     with the cards.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record RedrawCardsCommand(Guid DrawId, MixEnum Mix, IReadOnlyList<Guid> ChartIds)
        : IRequest<DrawDto>
    {
    }
}
