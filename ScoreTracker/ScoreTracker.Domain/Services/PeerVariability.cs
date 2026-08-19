using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     How far apart a peer group's scores sit on one chart, in five words
///     (docs/design/pumbility-overhaul.md D35). A scale of disagreement, not of quality.
/// </summary>
public enum PeerVariabilityLevel
{
    VeryConsistent,
    Consistent,
    Mixed,
    Split,
    VerySplit
}

/// <summary>
///     Bands the peers' interquartile widths into <see cref="PeerVariabilityLevel" /> — the tier
///     lists' standard-deviation technique applied to spread rather than count, and applied on the
///     log because raw widths skew hard enough that the bottom band never fires (measured skew
///     +1.10 raw against −0.25 on the log; §4.9). Pure.
///     <para>
///         Relative, deliberately: a chart is "split" against the other charts the same peers
///         play, which is the comparison a player makes when choosing between them. Band per chart
///         type, so a chart's word does not change when the page's pool selector does.
///     </para>
/// </summary>
public static class PeerVariability
{
    /// <summary>The five bands' cut points, in standard deviations from the mean of the log widths.</summary>
    public const double InnerCut = 0.5;

    public const double OuterCut = 1.5;

    /// <summary>The width a log point stands for: log(1 + width / 1000).</summary>
    public const double LogScale = 1000.0;

    /// <summary>
    ///     A level per chart, for every chart that has both quartiles. A single chart is Mixed by
    ///     construction (nothing to be split against); an empty input is an empty answer.
    /// </summary>
    public static IReadOnlyDictionary<Guid, PeerVariabilityLevel> Band(
        IEnumerable<(Guid ChartId, PhoenixScore Quartile1, PhoenixScore Quartile3)> quartiles)
    {
        var logs = quartiles
            .Select(q => (q.ChartId, Log: LogWidth((int)q.Quartile3 - (int)q.Quartile1)))
            .ToArray();
        var result = new Dictionary<Guid, PeerVariabilityLevel>();
        if (logs.Length == 0) return result;

        var mean = logs.Average(l => l.Log);
        var deviation = Math.Sqrt(logs.Sum(l => (l.Log - mean) * (l.Log - mean)) / logs.Length);
        foreach (var (chartId, log) in logs)
            result[chartId] = LevelFor(log, mean, deviation);
        return result;
    }

    /// <summary>log(1 + width / 1000); a width of zero is a log of zero.</summary>
    public static double LogWidth(int width)
    {
        return Math.Log(1 + Math.Max(0, width) / LogScale);
    }

    /// <summary>Where one log width falls against a population's mean and standard deviation.</summary>
    public static PeerVariabilityLevel LevelFor(double log, double mean, double deviation)
    {
        if (deviation <= 0) return PeerVariabilityLevel.Mixed;
        var z = (log - mean) / deviation;
        return z < -OuterCut ? PeerVariabilityLevel.VeryConsistent
            : z < -InnerCut ? PeerVariabilityLevel.Consistent
            : z <= InnerCut ? PeerVariabilityLevel.Mixed
            : z <= OuterCut ? PeerVariabilityLevel.Split
            : PeerVariabilityLevel.VerySplit;
    }
}
