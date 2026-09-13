using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     One patch of one mix, stamped on a chart: the number the game prints on its update notice
///     — <c>1.01.0</c> — with the mix it belongs to, the day it shipped in Korea and its place in
///     that mix's release order. A chart carries two. <see cref="Chart.AddedIn" /> is the patch of
///     the chart's own mix it entered in, so a carry-over reads the launch patch there; <see cref="Chart.Debut" />
///     is the patch of its origin mix it first appeared in anywhere, the same stamp on a debut and
///     an older mix's patch on a carry-over (docs/design/chart-versions.md §2).
///     <para>
///         <see cref="SortOrder" /> is the ordering truth within a mix. Names are never parsed for
///         order — <c>Pre-v1.10</c>, <c>JE</c> and <c>Release</c> are real names — and
///         <see cref="ReleaseDate" /> is null on a legacy patch nobody dated, which is why a
///         "through this version" question compares orders and only the date filter reads dates.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record VersionStamp(MixEnum Mix, string Version, DateOnly? ReleaseDate, int SortOrder);
