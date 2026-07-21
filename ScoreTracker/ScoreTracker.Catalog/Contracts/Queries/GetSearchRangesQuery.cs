using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The extents the SRP's range sliders travel over, measured from the scope's own
///     catalogue (docs/design/charts-srp.md) — a BPM slider spanning 40–300 when the
///     catalogue is 90–250 wastes most of its travel.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSearchRangesQuery(MixEnum Mix, bool AllMixes = false)
    : IQuery<ChartSearchRanges>;
