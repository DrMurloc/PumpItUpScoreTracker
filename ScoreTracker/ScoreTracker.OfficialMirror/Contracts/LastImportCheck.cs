namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     What the Score check panel needs to render on page load: the last verdict (null before a
///     player has ever run one), the deep scans left this calendar month, and the date the next
///     allowance arrives so an exhausted panel can name it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record LastImportCheck(ImportCheckReport? Report, int DeepScansLeft,
    DateTimeOffset NextScanUnlocksAt);
