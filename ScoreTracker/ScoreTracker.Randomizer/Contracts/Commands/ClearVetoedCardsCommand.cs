using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     The explicit compaction step: removes vetoed cards and renumbers the remainder
    ///     sequentially (the natural round boundary — order badges stay stable within one
    ///     veto discussion). Returns the compacted draw.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record ClearVetoedCardsCommand(Guid DrawId) : IRequest<DrawDto>
    {
    }
}
