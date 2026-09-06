using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     One chart of a March of Murlocs session as the board stored it: the chart, what was
///     scored on it, the session points it earned under the season's frozen configuration,
///     and the balanced level that priced it (the season snapshot's override where one
///     exists, the folder level + 0.5 where none does — docs/design/march-of-murlocs.md §4).
///     <see cref="PlayedAt" /> is the import's stamp when the session came from the journal;
///     hand-entered sessions carry none and their timeline is derived.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSessionChart(
    Chart Chart,
    PhoenixScore Score,
    PhoenixPlate Plate,
    bool IsBroken,
    int SessionScore,
    int BonusPoints,
    double BalancedLevel,
    DateTimeOffset? PlayedAt);

/// <summary>
///     The four numbers a session is described by (§11.6 — "Where the points came from"):
///     how many charts, how hard on average, how cleanly, and how much of the window went
///     unplayed. <see cref="AverageBalancedLevel" /> is the season's frozen balanced level,
///     which is what actually priced each chart; <see cref="AverageFolderLevel" /> is the
///     folder number beside it so the half-level gap never reads as a bug. The grade is the
///     grade of the average score, on the mix the board runs.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMLevers(
    int ChartsPlayed,
    double AverageBalancedLevel,
    double AverageFolderLevel,
    PhoenixScore AverageScore,
    PhoenixLetterGrade AverageGrade,
    TimeSpan Downtime,
    TimeSpan SongTime,
    int TotalScore)
{
    public int PointsPerChart => ChartsPlayed == 0 ? 0 : TotalScore / ChartsPlayed;
}

/// <summary>
///     A session chart placed on the clock: when it started inside the window, how long it
///     ran, and the points it earned per second of song — the pace chart's unit.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMTimedChart(MoMSessionChart Chart, TimeSpan StartsAt, TimeSpan Length, double PointsPerSecond);

/// <summary>
///     The season-comparison counterfactual (§11.3, D20): an older session re-priced under a
///     newer season's frozen configuration, with what moved split two ways. The charts and
///     the scores are the same; only the balance and the tables differ. The two parts
///     multiply, so <see cref="ChartsReRated" /> + <see cref="TablesReCut" /> is less than
///     <see cref="RepricedTotal" /> − <see cref="OldTotal" /> — the page says so rather than
///     printing three numbers that appear not to add up.
/// </summary>
/// <param name="OldTotal">The total the old season recorded, verbatim.</param>
/// <param name="RecomputedOldTotal">
///     The same session re-run under its own season today; equals <paramref name="OldTotal" />
///     unless the catalog moved under it (a song's length, a chart retired).
/// </param>
/// <param name="ChartsReRated">What the newer season's chart balance alone would have added.</param>
/// <param name="TablesReCut">What the newer season's scoring tables alone would have added.</param>
/// <param name="RepricedTotal">The old session priced entirely as the newer season.</param>
[ExcludeFromCodeCoverage]
public sealed record MoMRepricingSplit(
    int OldTotal,
    int RecomputedOldTotal,
    int ChartsReRated,
    int TablesReCut,
    int RepricedTotal)
{
    public int TotalShift => RepricedTotal - OldTotal;
}

/// <summary>
///     A chart two sessions have in common, with both sides' score and session points. A
///     total says who won; this says where.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMSharedChart(
    Chart Chart,
    PhoenixScore MyScore,
    PhoenixPlate MyPlate,
    bool MyBroken,
    int MyPoints,
    PhoenixScore TheirScore,
    PhoenixPlate TheirPlate,
    bool TheirBroken,
    int TheirPoints)
{
    /// <summary>Positive when my side earned more on this chart.</summary>
    public int Gap => MyPoints - TheirPoints;
}
