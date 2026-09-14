using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Contracts.Events;

/// <summary>
///     The roll stamped a season sealed (docs/design/seasons.md D13): its boundary is seven days
///     behind, and from now on no import, undo, replay or backfill writes a row carrying its number.
///     Nothing moves — the rows stay where they are under it (D14).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SeasonSealedEvent(SeasonId Season, DateTimeOffset SealedAt);
