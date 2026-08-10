using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>Why a mix has no title progress to show.</summary>
public enum TitleLadderAvailability
{
    /// <summary>The ladder exists and progress is real.</summary>
    Available,

    /// <summary>The mix awarded titles; this site has not built its ladder.</summary>
    NotYetBuilt,

    /// <summary>The mix never awarded titles, so there is nothing to build.</summary>
    NeverExisted
}

/// <summary>
///     Which mixes have a title ladder, and why the rest do not. The distinction is the
///     whole point: "we have not built this yet" is a promise, and "this mix never had
///     titles" is a fact about Pump It Up. Telling a Prex 3 player to come back later
///     would be a lie, and telling a Prime 2 player the concept never existed is worse.
/// </summary>
public static class TitleLadders
{
    /// <summary>
    ///     Titles arrived with Prime 2 (owner, 2026-08-09). Prime 2's ladder is buildable and
    ///     wanted; it is simply not built. Everything older predates the concept. The American
    ///     line rides with the older mixes: no ladder has ever been asked for there, and a
    ///     mix that never awarded a title reads the same either way.
    /// </summary>
    public static TitleLadderAvailability For(MixEnum mix)
    {
        return mix switch
        {
            MixEnum.XX or MixEnum.Phoenix or MixEnum.Phoenix2 => TitleLadderAvailability.Available,
            MixEnum.Prime2 => TitleLadderAvailability.NotYetBuilt,
            _ => TitleLadderAvailability.NeverExisted
        };
    }

    public static bool HasLadder(MixEnum mix)
    {
        return For(mix) == TitleLadderAvailability.Available;
    }
}
