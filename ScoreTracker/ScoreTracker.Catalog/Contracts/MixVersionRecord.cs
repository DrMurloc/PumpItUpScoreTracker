using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     One patch of a mix as a consumer sees it (docs/design/chart-versions.md §2, §3).
///     <para>
///         <see cref="SortOrder" /> is the ordering truth — "through this version" and "after this
///         version" compare on it, never on the name. <see cref="ReleaseDate" /> is null on a legacy
///         patch nobody dated, which only the date filter cares about. <see cref="ChartCount" /> is
///         how many charts first appeared in this patch, in this mix.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MixVersionRecord(
    MixEnum Mix,
    string Name,
    DateOnly? ReleaseDate,
    int SortOrder,
    int ChartCount);
