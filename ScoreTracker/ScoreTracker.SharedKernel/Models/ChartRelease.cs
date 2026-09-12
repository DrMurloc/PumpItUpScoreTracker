using System.Diagnostics.CodeAnalysis;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     The patch of a mix a chart first appeared in — <c>1.01.0</c>, the number the game prints on
///     its update notice — with the date it shipped in Korea and its place in the mix's release
///     order. Per mix, not per chart: the same chart id carries its debut patch in its debut mix
///     and the launch version in every mix it carried into (docs/design/chart-versions.md §2).
///     <para>
///         <see cref="SortOrder" /> is the ordering truth. Names are never parsed for order —
///         <c>Pre-v1.10</c>, <c>JE</c> and <c>Release</c> are real names — and
///         <see cref="ReleaseDate" /> is null on a legacy patch nobody dated, which is why a
///         "through this version" question compares orders and only the date filter reads dates.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartRelease(string Version, DateOnly? ReleaseDate, int SortOrder);
