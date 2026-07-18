namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     The Hot Streak category's pure rules. The push floor is the folder level at the
///     25th percentile (nearest-rank) of the player's rating-pool charts: suggesting
///     below it cannot rationally move the ratings those pools feed, which is how the
///     goal avoids offering an S9 to a D23 player. Caps are per render: a handful of
///     seeds, a handful of targets each — the widget is a shelf, not a catalog.
/// </summary>
internal static class HotStreakPolicy
{
    /// <summary>Seeds that may contribute targets in one result.</summary>
    public const int MaxSeeds = 6;

    /// <summary>Targets kept per contributing seed, best similarity first.</summary>
    public const int TargetsPerSeed = 4;

    /// <summary>Flagged rows scanned for seed candidates before giving up.</summary>
    public const int SeedScanCap = 200;

    private const double FloorPercentile = 0.25;

    /// <summary>
    ///     The folder level at the pool's 25th percentile, nearest-rank; 1 when the pool
    ///     is empty — no pool, no gate.
    /// </summary>
    public static int PushFloor(IReadOnlyCollection<int> poolLevels)
    {
        if (poolLevels.Count == 0) return 1;
        var sorted = poolLevels.OrderBy(l => l).ToArray();
        var rank = Math.Max(1, (int)Math.Ceiling(sorted.Length * FloorPercentile));
        return sorted[rank - 1];
    }
}
