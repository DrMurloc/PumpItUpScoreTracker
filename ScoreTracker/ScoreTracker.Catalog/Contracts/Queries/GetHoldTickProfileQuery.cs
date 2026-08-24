using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     The score calculator's holds section (docs/design/phoenix-score-calculator.md D11).
///     Computed from banked piucenter tap rows against the mix's note counts — Phoenix 2 counts
///     fall back to Phoenix where still unobserved (owner ruling, no disclaimer) — with the
///     re-step gates applied. Cached for a day: the inputs move weekly at most.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetHoldTickProfileQuery(MixEnum Mix) : IQuery<HoldTickProfile>;
