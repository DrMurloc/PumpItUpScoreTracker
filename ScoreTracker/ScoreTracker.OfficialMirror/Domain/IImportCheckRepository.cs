using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>A completeness check as it was stored, findings already deserialized.</summary>
internal sealed record ImportCheckRun(
    Guid Id,
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset RanAt,
    ImportCheckKind Kind,
    double OfficialPumbility,
    double LocalPumbility,
    int OfficialPasses,
    int LocalPasses,
    IReadOnlyList<CensusFinding> Findings);

internal enum ImportCheckKind
{
    /// <summary>The ~20-request per-level comparison. Free, and the only kind run by default.</summary>
    Census,

    /// <summary>A full walk of the best-score list. Rate limited — see <see cref="IImportCheckRepository" />.</summary>
    Deep
}

internal interface IImportCheckRepository
{
    Task Save(ImportCheckRun run, CancellationToken cancellationToken);

    /// <summary>The standing verdict the page renders on load, without touching piugame.</summary>
    Task<ImportCheckRun?> GetLatest(Guid userId, MixEnum mix, CancellationToken cancellationToken);

    /// <summary>
    ///     How many deep scans the user has spent in the calendar month containing
    ///     <paramref name="asOf" />. Census runs never count — the limit exists to stop repeated
    ///     full walks pointed at piugame, not to ration the cheap check.
    /// </summary>
    Task<int> CountDeepScansInMonth(Guid userId, DateTimeOffset asOf, CancellationToken cancellationToken);
}
