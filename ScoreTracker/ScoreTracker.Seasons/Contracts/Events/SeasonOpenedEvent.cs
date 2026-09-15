using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Seasons.Contracts.Events;

/// <summary>
///     The roll created a season's row (docs/design/seasons.md §7): the quarter the clock stood in
///     had none. Carries the window so a consumer never recomputes the calendar.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SeasonOpenedEvent(SeasonId Season, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
