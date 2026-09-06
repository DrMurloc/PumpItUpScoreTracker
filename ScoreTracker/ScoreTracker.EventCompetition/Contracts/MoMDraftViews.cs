using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>What happened to a play someone tried to enter (D45, march-of-murlocs.md §1).</summary>
public enum MoMEntryOutcome
{
    /// <summary>A new chart in the session.</summary>
    Added,

    /// <summary>The chart was already in and this play beat it, so it took its place.</summary>
    Replaced,

    /// <summary>The chart was already in with a score at least as high; nothing changed.</summary>
    Kept,

    /// <summary>The session could not take it at all: the window is full, or the chart prices zero.</summary>
    Rejected
}

/// <summary>
///     The result of entering one play. <see cref="PreviousScore" /> is what the session held on
///     that chart before, which is the only thing the page needs to say what just happened.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMEntryResult(MoMEntryOutcome Outcome, PhoenixScore? PreviousScore);

/// <summary>
///     A session being recorded (§11.4). A draft is a session with no publication stamp (D17), so
///     this is also what the published state renders from — <see cref="IsPublished" /> is the only
///     difference, and a published session is frozen.
///     <para>
///         <see cref="SongTime" /> and <see cref="SongTimeBeforeLast" /> are the budget bar: the
///         window governs when a chart may <em>start</em> (§1), so the bar fills with song time and
///         only the time before the closing chart can overflow it.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMDraftView(
    Guid SessionId,
    Guid BoardId,
    Guid SeasonId,
    string SeasonName,
    MixEnum Mix,
    ChartType ChartType,
    bool IsPublished,
    TimeSpan Window,
    TimeSpan SongTime,
    TimeSpan SongTimeBeforeLast,
    int TotalScore,
    Uri? VideoUrl,
    IReadOnlyList<MoMSessionChart> Charts)
{
    /// <summary>Nothing more can start: the charts before the last one already fill the window.</summary>
    public bool WindowFull => SongTimeBeforeLast >= Window;
}
