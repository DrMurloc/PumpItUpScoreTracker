using ScoreTracker.Domain.Records;

namespace ScoreTracker.Seasons.Contracts.Queries;

/// <summary>
///     Every season the roll has opened, most recent first — the admin console's list, and whatever
///     later slices need a calendar for. Read-only, never on the bus.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSeasonsQuery : IQuery<IReadOnlyList<SeasonRecord>>;
