namespace ScoreTracker.Domain.Services;

/// <summary>
///     When is a score "old"? A score is old only when it is BOTH past the grace floor
///     AND an age OUTLIER in the player's own record — beyond mean + 1σ of their score
///     ages, the same banding the Age lens uses. The grace floor exists ONLY so a new
///     account's three-week-old scores never read as outdated next to last week's — past
///     one month, the player's own distribution rules (the target is the years-old
///     one-and-done chart nobody revisits, not a calendar line). The outlier test is what
///     keeps a uniformly-old history (a returning player's coherent snapshot) at full
///     voice — with no age spread, nothing is an outlier. Outliers are DIMINISHED, never
///     excluded: half-voice per 180 days beyond the threshold, floored. Everything inside
///     the threshold keeps weight 1. Evidence-weighting, not value-weighting — old
///     ceiling scores still prove the skill, just more quietly.
///     One implementation on purpose: the tier-list blend, its materialized freshness,
///     and the Hot Streak "treat very old scores as unplayed" toggle must all agree on
///     what "old" means, or a stale baseline reads as a phantom deviation.
/// </summary>
public static class ScoreAgePolicy
{
    public const double AgeGraceDays = 30; // owner-locked: younger than this is never "old"
    public const double AgeOutlierStdDevs = 1.0;
    private const double DiminishHalfLifeDays = 180;
    private const double MinAgeWeight = 0.1;

    public static IReadOnlyDictionary<Guid, double> AgeOutlierWeights(
        IEnumerable<(Guid Key, DateTimeOffset RecordedDate)> scores, DateTimeOffset now)
    {
        var ages = scores
            .ToDictionary(s => s.Key, s => Math.Max(0, (now - s.RecordedDate).TotalDays));
        if (!ages.Any()) return new Dictionary<Guid, double>();
        var threshold = Math.Max(AgeGraceDays,
            ages.Values.Average() + AgeOutlierStdDevs * TierListProcessor.StdDev(ages.Values, false));
        return ages.ToDictionary(kv => kv.Key, kv => kv.Value <= threshold
            ? 1.0
            : Math.Max(MinAgeWeight, Math.Pow(0.5, (kv.Value - threshold) / DiminishHalfLifeDays)));
    }
}
