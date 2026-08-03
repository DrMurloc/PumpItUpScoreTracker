using System.Globalization;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The official site's own partition of an account, and how one of our charts maps into it.
///     The buckets are read off the page rather than assumed — the two mixes do not offer the same
///     set, and bucketing our side by a list the site did not actually serve is how a census
///     silently compares different denominators.
/// </summary>
internal static class CensusBuckets
{
    public const string All = "";
    public const string Over27 = "27over";
    public const string Over10 = "10over";
    public const string CoOp = "coop";

    /// <summary>
    ///     Not a bucket the site serves — the name we give the levels Phoenix's play-data page
    ///     refuses to break down ("Levels 1~9 are not included in the rating and are not shown"),
    ///     recovered as a residual against the best-score total. Phoenix 2 buckets those levels
    ///     properly and never needs it.
    /// </summary>
    public const string SubTen = "sub10";

    /// <summary>
    ///     <see cref="Over10" /> is the sum of every numeric bucket plus <see cref="Over27" />, and
    ///     <see cref="All" /> is the whole page — both overlap the per-level buckets, so counting
    ///     them as levels would double every chart.
    /// </summary>
    public static bool IsAggregate(string bucket)
    {
        return bucket is All or Over10;
    }

    /// <summary>
    ///     The buckets that actually partition the account, in the order the page offered them.
    /// </summary>
    public static IReadOnlyList<string> Partitioning(IEnumerable<string> offered)
    {
        return offered.Where(b => !IsAggregate(b)).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    ///     Which bucket one of our charts belongs to, given the buckets this mix's page offers.
    ///     CO-OP is its own bucket at every level; anything at or above the site's top numeric
    ///     bucket falls into 27-over; anything below its lowest is the sub-10 residual.
    /// </summary>
    public static string For(ChartType type, DifficultyLevel level, IReadOnlyCollection<string> offered)
    {
        if (type == ChartType.CoOp) return CoOp;

        var numeric = offered.Where(b => int.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(b => int.Parse(b, CultureInfo.InvariantCulture))
            .ToArray();
        if (numeric.Length == 0) return SubTen;

        var value = (int)level;
        if (value > numeric.Max()) return offered.Contains(Over27) ? Over27 : SubTen;
        if (value < numeric.Min()) return SubTen;
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
