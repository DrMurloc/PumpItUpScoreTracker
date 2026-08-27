using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Catalog.Contracts.Events;

/// <summary>
///     A piucenter ingestion finished and the folder baselines were rebuilt for
///     <paramref name="Mixes" />. Published by the crawl and the snapshot import alike — the
///     two paths through the same pipeline — so anything derived from the step analysis can
///     recompute without polling or a nightly job it does not need.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PiuCenterDataIngestedEvent(IReadOnlyList<MixEnum> Mixes);
