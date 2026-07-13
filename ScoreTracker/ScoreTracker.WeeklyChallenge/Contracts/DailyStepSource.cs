namespace ScoreTracker.WeeklyChallenge.Contracts;

/// <summary>
///     Where a Daily Step entry's score came from: an <see cref="Official" /> import event (verified),
///     or a <see cref="Manual" /> submission through the widget's Record popover (self-reported —
///     the only way to log a deliberate Limbo low-pass, since the ledger only ever keeps your best).
/// </summary>
public enum DailyStepSource
{
    Official,
    Manual
}
