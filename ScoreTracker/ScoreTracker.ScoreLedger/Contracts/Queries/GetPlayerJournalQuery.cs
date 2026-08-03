using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     A player's per-attempt history in one mix, newest first — every play the journal holds, not
///     just the ones that became records.
///     <para>
///         <c>GetChartScoreJourneyQuery</c> answers the same question for one chart and returns the
///         whole history unpaged, which is right for a chart page and unworkable for a player with
///         thousands of plays.
///     </para>
///     <para>
///         Paged by keyset, not offset: the journal is appended to while a caller walks it.
///         <see cref="BeforeOccurredAt" /> and <see cref="BeforeChartId" /> are the last row the
///         caller saw.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerJournalQuery(
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset? BeforeOccurredAt = null,
    Guid? BeforeChartId = null,
    DateTimeOffset? Since = null,
    int Limit = 100) : IQuery<IReadOnlyList<ScoreJournalEntry>>;
