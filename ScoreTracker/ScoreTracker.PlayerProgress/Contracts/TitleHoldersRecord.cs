using ScoreTracker.Domain.Models;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <param name="Holders">
///     Public holders standing on this rung — pool descending on the gem rungs that carry one,
///     by name everywhere else.
/// </param>
/// <param name="HiddenCount">
///     Holders standing here whose profile is private. Surfaced as a count so the drawer can say
///     why its list is shorter than the rarity figure beside it, without naming anyone.
/// </param>
/// <param name="ClimbedPastCount">
///     Players who hold this title and a higher rung of the same ladder. They are counted rather
///     than listed: on Intermediate Lv.1 that is very nearly the whole site, and a list of
///     everyone says nothing about who is actually standing here.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record TitleHoldersRecord(
    IReadOnlyList<TitleHolder> Holders,
    int HiddenCount,
    int ClimbedPastCount = 0);

/// <param name="TotalPumbility">
///     The holder's total-PUMBILITY pool, carried only on the eight [P.B] gem rungs, where the
///     drawer subdivides holders by level (docs/design/pumbility-levels.md). Raw and unrounded —
///     presentation rounds, nothing below it does. Null on every other title, and for a holder
///     with no Phoenix 2 stats row.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record TitleHolder(User User, double? TotalPumbility = null);
