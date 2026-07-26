using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     How many tracked players hold each title in a mix, and how many players are tracked at
///     all — the denominator, so a caller never divides by its own guess. One aggregate read
///     for the whole list: the titles page colours every rung by this, so it cannot be per-title.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetTitleRarityQuery(MixEnum Mix) : IQuery<TitleRarityRecord>;
