using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Contracts;

/// <summary>
///     How well the Planner assumes you play (docs/design/march-of-murlocs.md §11.5).
///     <para>
///         Deliberately not <c>PlayerProgress.Energy</c>, though the page borrows its copy. That
///         enum is peers all the way through, including its top rung — a score only one in four of
///         your peers beat. The Planner's top rung is <em>your own best</em>, which is a different
///         claim about a different population, and one enum meaning both would be a trap for
///         whoever reads it next.
///     </para>
/// </summary>
public enum MoMEnergy
{
    /// <summary>A score three in four of your peers reach.</summary>
    Good,

    /// <summary>The middle of your peers.</summary>
    Great,

    /// <summary>Your best on every chart. A ceiling, and the page says so.</summary>
    TopOfMyGame
}

/// <summary>
///     How hard the plan pushes: a cap on the level it will reach for, anchored to your last
///     session's average (§11.5). A plan built from your hardest charts is not a plan you can hold
///     for ninety minutes, which is what the cap is for.
/// </summary>
public enum MoMPush
{
    /// <summary>A level below your session average.</summary>
    Steady,

    /// <summary>Your session average.</summary>
    Push,

    /// <summary>No cap.</summary>
    AllOut
}

/// <summary>
///     One chart of your record book as the Planner prices it.
///     <para>
///         <see cref="IsProjected" /> separates a score you have actually set from one the peers
///         suggest you would: at Top of my game every row is your own, and at the other two rungs a
///         chart you have never passed can still be priced.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMPlanChartView(
    Chart Chart,
    PhoenixScore Score,
    PhoenixPlate? Plate,
    bool IsProjected,
    int Points,
    double PointsPerSecond,
    double BalancedLevel,
    bool InSet,
    bool IsClosing,
    RestChartFacts? Rest)
{
    /// <summary>A finisher: three minutes or more of song, which is what a closing slot is worth spending on.</summary>
    public bool IsFinisher => Chart.Song.Duration >= TimeSpan.FromMinutes(3);
}

/// <summary>
///     The Planner's answer (§11.5): your record book priced at one energy, the set the solver
///     suggests inside it, and the same four numbers the Season describes a played session by — so
///     what you are chasing and what you posted are described identically.
///     <para>
///         <see cref="BankedThisSeason" /> is your best published session on this board, present so
///         the page can print the conversion rate. The plan is a ceiling; the gap is the interesting
///         part, and nothing else on the site can tell a player what stamina costs them.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MoMPlanView(
    Guid BoardId,
    string SeasonName,
    MixEnum Mix,
    ChartType ChartType,
    TimeSpan Window,
    TimeSpan RestPerChart,
    MoMEnergy Energy,
    MoMPush Push,
    int? LevelCap,
    double? Anchor,
    int ProjectedPoints,
    int ChartsPlanned,
    double AverageBalancedLevel,
    PhoenixScore AverageScore,
    TimeSpan Downtime,
    int? BankedThisSeason,
    IReadOnlyList<MoMPlanChartView> Charts)
{
    /// <summary>What you actually banked as a share of what the book plans, or null with nothing banked.</summary>
    public double? Conversion =>
        BankedThisSeason is { } banked && ProjectedPoints > 0 ? banked / (double)ProjectedPoints : null;
}
