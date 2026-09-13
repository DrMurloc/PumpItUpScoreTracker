namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     Turns the four ways of asking for versions — a set of names, "through this one", "after this
///     one", "released after this date" — into the one thing every consumer filters on: the set of
///     version names a chart's <c>Release</c> must be in. The API, the /Charts drawer and the
///     randomizer all resolve through here so "through 2.09.0" means the same rows everywhere
///     (docs/design/chart-versions.md §3, §4).
///     <para>
///         The filters combine with AND. A chart with no version never matches any of them. The
///         date filter skips patches with no date, because an undated patch is not "after" anything.
///     </para>
/// </summary>
public static class MixVersionRange
{
    /// <summary>
    ///     Resolves the filters against the mix's version list. <paramref name="names" /> is null
    ///     when no filter was asked for, otherwise the (possibly empty) set of matching names.
    ///     Returns false, naming the offender in <paramref name="unknownName" />, when a version
    ///     filter names a patch the mix does not have.
    /// </summary>
    public static bool TryResolve(IReadOnlyList<MixVersionRecord> versions,
        IReadOnlyCollection<string>? inVersions, string? byVersion, string? afterVersion, DateOnly? releasedAfter,
        out IReadOnlySet<string>? names, out string? unknownName)
    {
        names = null;
        unknownName = null;
        var asked = inVersions is { Count: > 0 } || byVersion != null || afterVersion != null || releasedAfter != null;
        if (!asked) return true;

        var byName = versions.ToDictionary(v => v.Name, v => v, StringComparer.Ordinal);
        var matching = versions.Select(v => v.Name).ToHashSet(StringComparer.Ordinal);

        unknownName = inVersions is { Count: > 0 } ? KeepNamed(inVersions, byName, matching) : null;
        unknownName ??= byVersion is null ? null : KeepThrough(byVersion, byName, versions, matching);
        unknownName ??= afterVersion is null ? null : KeepAfter(afterVersion, byName, versions, matching);
        if (unknownName != null) return false;

        if (releasedAfter != null)
            matching.IntersectWith(versions
                .Where(v => v.ReleaseDate != null && v.ReleaseDate.Value > releasedAfter.Value)
                .Select(v => v.Name));

        names = matching;
        return true;
    }

    /// <summary>Narrows to the named patches; returns the first name the mix does not have, or null.</summary>
    private static string? KeepNamed(IEnumerable<string> inVersions, IReadOnlyDictionary<string, MixVersionRecord> byName,
        HashSet<string> matching)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in inVersions)
        {
            var name = raw.Trim();
            if (!byName.ContainsKey(name)) return name;
            wanted.Add(name);
        }

        matching.IntersectWith(wanted);
        return null;
    }

    /// <summary>Narrows to everything up to and including the patch; returns the name when the mix lacks it.</summary>
    private static string? KeepThrough(string byVersion, IReadOnlyDictionary<string, MixVersionRecord> byName,
        IReadOnlyList<MixVersionRecord> versions, HashSet<string> matching)
    {
        var name = byVersion.Trim();
        if (!byName.TryGetValue(name, out var by)) return name;
        matching.IntersectWith(versions.Where(v => v.SortOrder <= by.SortOrder).Select(v => v.Name));
        return null;
    }

    /// <summary>Narrows to everything strictly after the patch; returns the name when the mix lacks it.</summary>
    private static string? KeepAfter(string afterVersion, IReadOnlyDictionary<string, MixVersionRecord> byName,
        IReadOnlyList<MixVersionRecord> versions, HashSet<string> matching)
    {
        var name = afterVersion.Trim();
        if (!byName.TryGetValue(name, out var after)) return name;
        matching.IntersectWith(versions.Where(v => v.SortOrder > after.SortOrder).Select(v => v.Name));
        return null;
    }

    /// <summary>Every version up to and including the named one, in release order — the bulk select behind a chip row.</summary>
    public static IReadOnlyList<string> Through(IReadOnlyList<MixVersionRecord> versions, string name)
    {
        var bound = versions.FirstOrDefault(v => string.Equals(v.Name, name.Trim(), StringComparison.Ordinal));
        if (bound == null) return Array.Empty<string>();
        return versions.Where(v => v.SortOrder <= bound.SortOrder).OrderBy(v => v.SortOrder).Select(v => v.Name).ToArray();
    }

    /// <summary>
    ///     What a picked set reads as: null for nothing picked, the name of the last version when the
    ///     set is exactly the versions up to it (one "Through v2.09.0" chip), otherwise null again so
    ///     the caller lists the picks one by one.
    /// </summary>
    public static string? ThroughName(IReadOnlyList<MixVersionRecord> versions, IReadOnlyCollection<string> picked)
    {
        if (picked.Count < 2) return null;
        var ordered = versions.OrderBy(v => v.SortOrder).Select(v => v.Name).ToArray();
        var prefix = ordered.Take(picked.Count).ToArray();
        return prefix.Length == picked.Count && prefix.All(name => picked.Contains(name)) ? prefix[^1] : null;
    }
}
