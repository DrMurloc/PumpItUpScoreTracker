using ScoreTracker.Domain.Services;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     How the player says they are playing today (docs/design/pumbility-overhaul.md D51): which
///     rung of their peers' scores every projected score and gain on the PUMBILITY page reads.
///     Good is the default and the rung everything off that page reads (D50).
/// </summary>
public enum Energy
{
    /// <summary>The peers' first quartile — a score three in four of them reach.</summary>
    Good,

    /// <summary>The median — the middle of the peers.</summary>
    Great,

    /// <summary>The third quartile — a score only one in four of the peers beat.</summary>
    TopOfMyGame
}

/// <summary>The rung each energy reads, and the set a sweep computes so any of them is a lookup.</summary>
[ExcludeFromCodeCoverage]
public static class EnergyRungs
{
    /// <summary>Every rung the page can ask for, in the order the chip offers them.</summary>
    public static readonly IReadOnlyCollection<double> All = new[]
    {
        Energy.Good.Quantile(), Energy.Great.Quantile(), Energy.TopOfMyGame.Quantile()
    };

    public static double Quantile(this Energy energy)
    {
        return energy switch
        {
            Energy.Great => PeerEstimator.Median,
            Energy.TopOfMyGame => PeerEstimator.UpperQuartile,
            _ => PeerEstimator.DefaultQuantile
        };
    }
}
