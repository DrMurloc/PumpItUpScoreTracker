namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     Per-mix capability flags. Started with the tier-list surfaces (tier-lists overhaul
///     design doc §8) and now also answers what the nav should advertise.
///
///     The nav flags are NOT the route gate coming back. That gate blocked routes off one
///     flag and hid the whole site behind it; every route here stays reachable, and a page a
///     mix cannot answer still explains itself on arrival. These decide something narrower —
///     whether it is honest to put the link in front of someone. Offering PUMBILITY to a
///     Prime 2 player offers a number that does not exist for them.
///
///     They live here, together, because the desktop menu and the phone's More sheet are two
///     renderings of one set of rules, and two copies of a rule is precisely how the
///     recording form lost its legacy branch (docs/design/legacy-mixes.md).
///
///     Each answer is the mix's profile's (<see cref="MixProfiles" />), not a guess off its
///     scoring model: a Phoenix-scored mix with no PUMBILITY formula exists now.
/// </summary>
public static class MixCapabilities
{
    /// <summary>PUMBILITY prices a 1,000,000-scale score with a formula the mix has; no older mix has one.</summary>
    public static bool HasPumbility(this MixEnum mix)
    {
        return MixProfiles.For(mix).Features.Pumbility;
    }

    /// <summary>The season recap is computed from Phoenix 1 data only, for now (owner, 2026-08-10).</summary>
    public static bool HasRecap(this MixEnum mix)
    {
        return mix == MixEnum.Phoenix;
    }

    /// <summary>
    ///     The official site publishes boards for the current generation only. The pages clamp
    ///     to Phoenix if reached directly; the nav simply does not offer them.
    /// </summary>
    public static bool HasOfficialBoards(this MixEnum mix)
    {
        return MixProfiles.For(mix).Features.OfficialBoards;
    }

    /// <summary>
    ///     Whether a weekly board rotates for this mix. Every table behind the feature is
    ///     already keyed per mix and the rollup runs on any of them, but rotation publishes to
    ///     the Phoenix generation only — and the entry type cannot hold an era score yet
    ///     (docs/design/legacy-mixes.md). Off the nav until it can, rather than a link to a
    ///     board that will not exist for a while.
    /// </summary>
    public static bool HasWeeklyBoard(this MixEnum mix)
    {
        return MixProfiles.For(mix).Features.WeeklyBoard;
    }

    /// <summary>
    ///     March of Murlocs is a Phoenix-lineage event (docs/design/march-of-murlocs.md D19): it prices a
    ///     1,000,000-scale score against a chart's level, which no legacy mix has. Phoenix 2 has the
    ///     section too — its boards open after the scoring session, and the page says so (D12).
    /// </summary>
    public static bool HasMarchOfMurlocs(this MixEnum mix)
    {
        return MixProfiles.For(mix).Features.MarchOfMurlocs;
    }

    /// <summary>
    ///     The Phoenix score and rating calculators answer Phoenix questions. The lifebar
    ///     calculator and the mix diff are deliberately absent: neither reads the selected mix,
    ///     so both stand on every mix.
    /// </summary>
    public static bool HasPhoenixCalculators(this MixEnum mix)
    {
        return MixProfiles.For(mix).Features.PhoenixCalculators;
    }

    /// <summary>
    ///     Whether scores on this mix are also tracked against a quarterly season
    ///     (docs/design/seasons.md §1): <b>Phoenix 2 only. Phoenix 1 has no seasons and never will</b>
    ///     — it is going offline-only. Named here rather than repeated as a local array in each
    ///     vertical that needs it, because the read side and the write side disagreeing about which
    ///     mixes have seasons produces rows nothing ever recomputes.
    /// </summary>
    public static bool HasSeasons(this MixEnum mix)
    {
        return mix == MixEnum.Phoenix2;
    }
}
