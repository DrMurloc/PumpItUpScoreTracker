using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Contracts.Events;

/// <summary>
///     One season wants its pool rebuilt from the journal (docs/design/seasons.md D23). The Ledger
///     replays the window into seasonal bests and answers with <c>SeasonalBestsBackfilledEvent</c>,
///     which Progression rolls up into standings. Carries the window so a consumer never has to
///     recompute the calendar, and so a season whose row moved cannot be replayed against the wrong
///     dates.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SeasonBackfillRequestedEvent(SeasonId Season, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
