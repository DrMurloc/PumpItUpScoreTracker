namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The debut seen-set, assembled from two sources. Everyone on last week's finished boards has
///     been seen by definition, so only this week's other players need checking against the weeks
///     before that — a handful of lookups instead of a read of the whole history.
/// </summary>
internal static class SeenPlayers
{
    /// <summary>This week's players who were not on last week's boards.</summary>
    public static IReadOnlyCollection<int> ToCheck(IReadOnlyList<PlacementRow> current,
        IReadOnlyList<PlacementRow>? previous)
    {
        var lastWeek = Ids(previous);
        return current.Select(p => p.PlayerId).Where(id => !lastWeek.Contains(id)).Distinct().ToArray();
    }

    /// <summary>Last week's players, plus whichever of the checked players an earlier week holds.</summary>
    public static IReadOnlySet<int> Combine(IReadOnlyList<PlacementRow>? previous, IReadOnlySet<int> seenEarlier)
    {
        var seen = Ids(previous);
        seen.UnionWith(seenEarlier);
        return seen;
    }

    private static HashSet<int> Ids(IReadOnlyList<PlacementRow>? placements) =>
        placements?.Select(p => p.PlayerId).ToHashSet() ?? new HashSet<int>();
}
