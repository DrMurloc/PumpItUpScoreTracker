using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Domain;

/// <summary>A patch of a mix as the catalog knows it (docs/design/chart-versions.md §2).</summary>
[ExcludeFromCodeCoverage]
internal sealed record MixVersion(Guid Id, MixEnum Mix, string Name, DateOnly? ReleaseDate, int SortOrder);

/// <summary>
///     The mix's patches. Reads are the whole list, oldest first — thirty rows at most — and the
///     one write appends a patch at the end of the order, which is where a patch created from the
///     admin tool always belongs.
/// </summary>
internal interface IMixVersionRepository
{
    Task<IReadOnlyList<MixVersion>> GetVersions(MixEnum mix, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates the patch with the next sort order, or returns the existing row's id when the
    ///     mix already has a patch by that name — a batch re-run must not mint a second 1.01.0.
    /// </summary>
    Task<Guid> Create(MixEnum mix, string name, DateOnly? releaseDate, CancellationToken cancellationToken = default);
}
