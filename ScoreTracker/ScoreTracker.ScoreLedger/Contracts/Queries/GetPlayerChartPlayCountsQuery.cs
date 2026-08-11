using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     How many plays the journal holds for this player, per chart, in one mix. Charts with no
///     journal row are absent rather than zero.
///     <para>
///         <b>Only honest on Phoenix 2.</b> The journal shipped 2026-06-12 and Phoenix 2's first
///         import was 2026-07-11, so that mix has no backfill rows and its journal is a
///         gap-free personal play log. Phoenix is the opposite: the overwhelming majority of
///         its rows came from the 2026-06 backfill, one per record dated at that record's last
///         update, so a count there reads 1 for a chart played two hundred times.
///     </para>
///     <para>
///         Even where it is honest it counts plays PIU Scores <i>observed</i> — best-list
///         changes plus whatever the recently-played window caught — not every play of the
///         chart.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerChartPlayCountsQuery(Guid UserId, MixEnum Mix)
    : IQuery<IReadOnlyDictionary<Guid, int>>;
