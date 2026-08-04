using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     Board scores for a set of tags across a set of charts, from the latest sealed snapshot.
///     Batched because a rivals board asks about dozens of charts for dozens of tags at once
///     (docs/design/rivals.md §2.5).
///     <para>
///         This is the entire ceiling on what a board-only player can be shown: the mirror covers
///         level 20+ charts, roughly 300 deep, as of the last weekly seal. A chart with no
///         mirrored board, or a player who placed below its depth, is simply absent — the caller
///         renders nothing rather than a zero.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOfficialScoresForTagsQuery(
    MixEnum Mix,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<Guid> ChartIds) : IQuery<OfficialTagScores>;
