using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     What a player's Phoenix 1 record is worth under Phoenix 2's rules
///     (docs/design/pumbility-overhaul.md §5). Not their Phoenix 2 PUMBILITY — what it would
///     become if they re-cleared everything to the same standard, which is a different claim
///     and the page must say so.
/// </summary>
/// <param name="Projected">The repriced pool total.</param>
/// <param name="Bar">What the 50th chart of that repriced pool is worth.</param>
/// <param name="ScoredHere">How many scores they actually hold in Phoenix 2 today.</param>
/// <param name="NotYetScored">Pool charts they have not scored in Phoenix 2.</param>
/// <param name="ProjectedTitles">
///     Where this record would land on each of the three PUMBILITY ladders. Not titles held —
///     §8.2 — which is why every consumer of this says "would land you" and prints the pool
///     value that put it there.
/// </param>
/// <param name="SinglesInPool">How many of the repriced fifty are Singles.</param>
/// <param name="Phoenix1SinglesInPool">
///     How many Singles were in the Phoenix 1 pool. The pair is the point: Phoenix 2 prices a
///     Singles chart one level up, so a doubles pool can become a singles pool and the player's
///     whole grind priority inverts.
/// </param>
/// <param name="Entries">The repriced pool — the top fifty, which is what PUMBILITY means.</param>
/// <param name="Candidates">
///     Repriced scores ranked past the fiftieth. They are NOT the pool and never count toward
///     its figures, but they are still scores the player has actually hit: against an empty
///     Phoenix 2 pool a repriced #73 can beat the bar comfortably. Capping suggestions at the
///     pool hid exactly the rows carrying the best evidence there is (owner, 2026-08-06).
/// </param>
[ExcludeFromCodeCoverage]
public sealed record Phoenix2CarryoverRecord(
    double Projected,
    double Bar,
    int ScoredHere,
    int NotYetScored,
    IReadOnlyList<ProjectedTitle> ProjectedTitles,
    int SinglesInPool,
    int DoublesInPool,
    int Phoenix1SinglesInPool,
    int Phoenix1DoublesInPool,
    IReadOnlyList<CarryoverEntry> Entries,
    IReadOnlyList<CarryoverEntry> Candidates);

/// <summary>
///     Where a repriced pool would land on one PUMBILITY ladder.
///     <para>
///         ⚠ This is not a title held, and no surface may render it as one. At a mix launch it
///         and the rails on Your Pool say opposite things about the same three ladders — you
///         hold nothing yet, and your Phoenix 1 record is worth a gem — and that contrast is the
///         panel's whole argument. The wording carries the difference: "would land you", with
///         the pool value that put it there beside it (§8.2).
///     </para>
/// </summary>
/// <param name="Next">What the rung above it asks, or null at the top of the ladder.</param>
[ExcludeFromCodeCoverage]
public sealed record ProjectedTitle(PumbilityPool Pool, double Value, string? Title, int? Next);

/// <summary>
///     One Phoenix 1 score repriced for Phoenix 2, with what it would be worth and whether the
///     player has since scored the chart here.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CarryoverEntry(int Place, Guid ChartId, PhoenixScore Phoenix1Score,
    PhoenixLetterGrade Phoenix1Grade, double Phoenix2Value, PhoenixScore? Phoenix2Score, bool AvailableInPhoenix2);
