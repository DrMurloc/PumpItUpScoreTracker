using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>A night of this player's that already reached a board, and where it landed.</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMRecordedNight(Guid SessionId, ChartType ChartType, int Place, int Of, int TotalScore);

/// <summary>
///     A stretch of the night that would make a session (D32): one chart type, the length of a
///     session, under fifty minutes of rest. The times are what Submit's import would be handed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionCandidate(
    Guid BoardId,
    ChartType ChartType,
    int Charts,
    TimeSpan SongTime,
    TimeSpan Rest,
    DateTimeOffset From,
    DateTimeOffset To,
    int StageBreaks);

/// <summary>
///     What My Sessions offers a night (docs/design/march-of-murlocs.md D32, §11.4). Three states,
///     and they are ordered: a night already on a board wears a chip, a night holding a
///     session-shaped window gets the loud callout, and everything else gets the quiet link — which
///     is always there, because there is always a season.
///     <para>
///         <see cref="RecordBoardId" /> is the live board for this mix and chart type, absent only
///         when the daily job has not seated one yet. MoM never links back to My Sessions; this is
///         the one-way door in.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMOnRamp(Guid? RecordBoardId, MoMRecordedNight? Recorded, MoMSessionCandidate? Candidate);
