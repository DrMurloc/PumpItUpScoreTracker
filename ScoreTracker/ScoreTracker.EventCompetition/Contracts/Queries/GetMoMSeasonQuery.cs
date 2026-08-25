namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Resolves one season. All selectors null = the live season, falling back to the most
///     recently ended one between a season's end and the next cycle tick. (Year, Quarter)
///     names a quarterly season; LegacyName matches an off-grid legacy season's stored name
///     case-insensitively, with hyphens equal to spaces so URL segments resolve directly.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMSeasonQuery(int? Year = null, int? Quarter = null,
    string? LegacyName = null) : IQuery<MoMSeasonView?>;
