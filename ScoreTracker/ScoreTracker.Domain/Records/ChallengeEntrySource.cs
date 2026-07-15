namespace ScoreTracker.Domain.Records;

/// <summary>
///     Where a challenge-board entry's ranked score came from — shared by the Daily Step and
///     Weekly boards (weekly-charts-overhaul.md M5). <see cref="Official" /> is an import event
///     (renders the verified tag); <see cref="Manual" /> is a self-report through a Record
///     dialog (renders no tag; an attached photo upgrades the display tier to photo-proof
///     without changing the source). Lives beside the entry records because the weekly
///     repository port carries it.
/// </summary>
public enum ChallengeEntrySource
{
    Official,
    Manual
}
