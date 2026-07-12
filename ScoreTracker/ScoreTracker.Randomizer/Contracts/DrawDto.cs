using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Contracts
{
    /// <summary>
    ///     A draw is the current card set for a context (a user's personal draw or a
    ///     tournament's active draw). Cards carry per-pull identity (PullId) so
    ///     protect/veto state survives repeats and concurrent staff devices; Order is
    ///     stable (1-based) — the gold number badge players call out.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record DrawDto(Guid Id, Guid Slug, MixEnum Mix, Guid? TournamentId,
        IReadOnlyList<DrawCardDto> Cards)
    {
    }

    [ExcludeFromCodeCoverage]
    public sealed record DrawCardDto(Guid PullId, Guid ChartId, int Order, DrawCardState State)
    {
    }
}
