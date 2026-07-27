using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     The hub's snapshot-keyed cache entries. A sealed snapshot is immutable, so keying on
///     its id is normally enough to never stale — but anything that writes MORE rows onto an
///     already-sealed snapshot has to evict by hand, which is why these formats are shared
///     rather than private to the reader.
/// </summary>
internal static class OfficialCacheKeys
{
    public static string SnapshotStats(MixEnum mix, int snapshotId)
    {
        return $"OfficialSnapshotStats__{mix}__{snapshotId}";
    }

    public static string WhatItTakes(MixEnum mix, string type, int snapshotId)
    {
        return $"OfficialWhatItTakes__{mix}__{type}__{snapshotId}";
    }

    /// <summary>The board types <see cref="WhatItTakes" /> is asked for, for bulk eviction.</summary>
    public static readonly string[] WhatItTakesTypes = { "All", "Singles", "Doubles" };
}
