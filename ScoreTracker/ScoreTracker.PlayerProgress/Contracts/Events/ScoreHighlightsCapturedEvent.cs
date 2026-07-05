using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Events;

/// <summary>
///     Published after highlight capture for a score batch — always, even when nothing
///     was flagged. Downstream announcement consumers (the Discord score cards)
///     subscribe to THIS rather than the raw <c>PlayerScoresUpdatedEvent</c> so their
///     highlight flags are deterministic instead of racing capture. Carries the
///     original change set enriched per chart.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ScoreHighlightsCapturedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    MixEnum Mix,
    Guid? SessionId,
    IReadOnlyList<ScoreHighlightsCapturedEvent.HighlightedChange> Changes)
{
    public static ScoreHighlightsCapturedEvent Create(DateTimeOffset occurredAt, Guid userId, MixEnum mix,
        Guid? sessionId, IReadOnlyList<HighlightedChange> changes)
    {
        return new ScoreHighlightsCapturedEvent(Guid.NewGuid(), occurredAt, userId, mix, sessionId, changes);
    }

    [ExcludeFromCodeCoverage]
    public sealed record HighlightedChange(
        Guid ChartId,
        bool IsNewPass,
        int? OldScore,
        int? NewScore,
        string? Plate,
        bool IsBroken,
        HighlightFlag Flags);
}
