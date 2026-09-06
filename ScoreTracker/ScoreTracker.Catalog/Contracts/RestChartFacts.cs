namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     Whether a chart is a rest chart, and the five measurements that decided it — each against the
///     chart's own folder (mix, chart type, level), because "few steps" only means anything relative
///     to what else sits at that level (docs/design/march-of-murlocs.md D29).
///     <para>
///         The flags exist so a shelf can say <em>why</em> without re-deriving anything: a chart is a
///         rest chart when every one of them is true, and a consumer that wants to explain a near
///         miss reads which one is not.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RestChartFacts(
    Guid ChartId,
    bool IsRest,
    /// <summary>Steps per second, and where that sits in the folder. Rest wants the bottom quarter.</summary>
    double StepsPerSecond,
    int StepsPercentile,
    bool FewSteps,
    /// <summary>The share of judgements that are held rather than stepped. Rest wants the top quarter.</summary>
    double HoldShare,
    int HoldPercentile,
    bool HoldHeavy,
    /// <summary>No drills and no anchor runs at all — not "few", none.</summary>
    bool NoDrills,
    /// <summary>Over-90 and far twists together, as a share of the chart. Rest allows up to half.</summary>
    double HardTwistShare,
    bool SoftTwists,
    /// <summary>Crux density, and where it sits. Rest wants no higher than the folder's 60th percentile.</summary>
    double CruxDensity,
    int CruxPercentile,
    bool SoftCrux);
