using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Commands
{
    /// <summary>
    ///     Marks a pulled card Protected/Vetoed, or None to undo. Per-card rows mean two
    ///     staff devices acting at once merge instead of clobbering each other.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record SetDrawCardStateCommand(Guid DrawId, Guid PullId, DrawCardState State) : IRequest
    {
    }
}
