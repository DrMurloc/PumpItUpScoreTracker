using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events;

/// <summary>
///     Fired by the official-import ACL when a player's recent plays include the current Daily Step
///     chart. Carries both the player's best recent score and their lowest PASSING recent score on
///     that chart; the WeeklyChallenge consumer — which owns the board — picks the best on a normal
///     day and the lowest passing on a Limbo day. LowestPass* is null when no recent run passed.
///     Deliberately targeted (one chart per import) rather than shipping every attempt for every
///     chart.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record DailyStepScoreObservedEvent(
    Guid UserId,
    MixEnum Mix,
    Guid ChartId,
    int BestScore,
    string BestPlate,
    bool BestIsBroken,
    int? LowestPassScore,
    string? LowestPassPlate);
