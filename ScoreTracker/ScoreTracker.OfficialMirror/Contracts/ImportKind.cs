namespace ScoreTracker.OfficialMirror.Contracts;

/// <summary>
///     Which of the Import Scores page's three piugame reads a run was. Recorded because the
///     three cost wildly different amounts of the official site — a deep scan walks every
///     best-score page — so "deep scans time out" and "everything times out" are different
///     problems and have to be countable apart.
/// </summary>
public enum ImportKind
{
    /// <summary>The Import button: the best-score pages plus the recently-played list.</summary>
    Standard,

    /// <summary>Import and check: a standard import, then a level-by-level census, then a re-read of whatever disagrees.</summary>
    Check,

    /// <summary>Deep scan: a standard import, then every best-score page, no census first.</summary>
    DeepScan
}
