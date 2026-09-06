using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     One recent play as the import dialog draws it. A play that cannot count is still drawn, dimmed,
///     with the reason beside it — the choice has to be legible rather than magic (§11.4).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMImportPlay(
    int Index,
    Chart Chart,
    DateTimeOffset PlayedAt,
    PhoenixScore? Score,
    PhoenixPlate? Plate,
    bool IsBroken,
    bool IsStageBroken,
    bool WrongType);

/// <summary>
///     A contiguous run of plays with no break longer than fifteen minutes inside it.
///     <see cref="GapBefore" /> is the break that separated it from the block above, which the dialog
///     prints between them ("3h 42m gap").
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMImportBlock(
    int StartIndex,
    int EndIndex,
    DateTimeOffset From,
    DateTimeOffset To,
    int Plays,
    TimeSpan SongTime,
    TimeSpan RestTime,
    bool MixedTypes,
    TimeSpan? GapBefore);

/// <summary>
///     D10's three checks over the selected range, and the counts printed under them.
///     <para>
///         <see cref="OverWindowBeforeLast" /> is the hard block — the Add button disables. Song time
///         may still exceed the window without it, because the closing chart is allowed to overhang.
///         <see cref="SpanOverWindow" /> is only a warning, so it names the longest break rather than
///         gesturing at one: telling someone to trim an end is useless when the break is in the middle.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMImportChecks(
    int Charts,
    int Points,
    TimeSpan SongTime,
    bool OverWindowBeforeLast,
    TimeSpan Span,
    bool SpanOverWindow,
    TimeSpan? LongestBreak,
    Name? LongestBreakBefore,
    DateTimeOffset? LongestBreakAt,
    IReadOnlyList<Name> StageBreaksSkipped,
    int WrongTypeSkipped,
    int RepeatPlays);

/// <summary>
///     What the import dialog opens on: the recent plays, split into blocks at the long gaps, with a
///     selection pre-set to the block that would put the most charts on the board, and the checks for
///     whatever range is selected now.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMImportCandidates(
    ChartType ChartType,
    TimeSpan Window,
    IReadOnlyList<MoMImportPlay> Plays,
    IReadOnlyList<MoMImportBlock> Blocks,
    int SelectedStart,
    int SelectedEnd,
    MoMImportChecks Checks);

/// <summary>A play the import kept over one already in the session, and the score it displaced (D45).</summary>
[ExcludeFromCodeCoverage]
public sealed record MoMReplacedPlay(Guid ChartId, PhoenixScore PreviousScore);

/// <summary>
///     What an import did. <see cref="Kept" /> counts plays that lost to something already in the
///     session, so added + kept + skipped is every play the range offered.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMImportResult(
    int Added,
    int Replaced,
    int Kept,
    int Skipped,
    IReadOnlyList<MoMReplacedPlay> Replacements);
