namespace ScoreTracker.ScoreLedger.Contracts.Messages;

/// <summary>
///     Re-solves the max combo of every judged record and journal row, one player at a time,
///     from each play's score, its five judgement counts and the catalog's note count
///     (docs/design/stage-breaks-and-max-combo.md D15). Every judged row is re-derived, not
///     only the empty ones, so a corrected note count catches up on the next press. Idempotent;
///     the /Admin button publishes it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillMaxCombosCommand;
