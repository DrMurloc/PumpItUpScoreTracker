using MediatR;

namespace ScoreTracker.Randomizer.Contracts.Queries
{
    /// <summary>
    ///     Spectator read: anonymous, read-only, keyed by the unguessable slug in the
    ///     share link. Powers /Randomizer/Live/{slug}.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetDrawBySlugQuery(Guid Slug) : IQuery<DrawDto?>
    {
    }
}
