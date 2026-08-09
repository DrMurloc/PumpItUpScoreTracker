using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     One row per public player who has ever been observed CLEARING this chart, carrying their
///     lowest such score — the limbo board (docs/design/limbo-leaderboard.md). Read off the
///     append-only journal rather than the record, because the record only ever holds a player's
///     best and a low pass never displaces a higher one.
///     <para>
///         Ordered ascending and capped at <paramref name="Limit" />: past the interesting end sit
///         players whose only journaled pass came from the best-page walk or the 2026-06 backfill,
///         who are on the board at their best score and mean nothing there (D6).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetLowestPassingScoresQuery(Guid ChartId, MixEnum Mix, int Limit = 100)
    : IQuery<IEnumerable<UserPhoenixScore>>
{
}
