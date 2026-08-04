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
    /// <summary>
    ///     Stats for one reading of one snapshot. The reading is part of the key because the
    ///     two readings answer the same question differently, and because the roll-up writes
    ///     supplemented rows onto an already-sealed snapshot — the exact case a
    ///     snapshot-id-only key cannot survive.
    /// </summary>
    public static string SnapshotStats(MixEnum mix, int snapshotId, bool supplemented)
    {
        return $"OfficialSnapshotStats__{mix}__{snapshotId}__{Reading(supplemented)}";
    }

    public static string WhatItTakes(MixEnum mix, string type, int snapshotId)
    {
        return $"OfficialWhatItTakes__{mix}__{type}__{snapshotId}";
    }

    /// <summary>The board types <see cref="WhatItTakes" /> is asked for, for bulk eviction.</summary>
    public static readonly string[] WhatItTakesTypes = { "All", "Singles", "Doubles" };

    /// <summary>Both readings, so an evicting writer can clear the pair without guessing.</summary>
    public static readonly bool[] Readings = { false, true };

    private static string Reading(bool supplemented) => supplemented ? "supp" : "official";
}
