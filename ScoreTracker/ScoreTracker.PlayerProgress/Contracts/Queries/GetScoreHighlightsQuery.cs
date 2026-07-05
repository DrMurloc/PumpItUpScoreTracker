using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>Windowed read of a player's captured noteworthy-score flags.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetScoreHighlightsQuery(Guid UserId, MixEnum Mix, DateTimeOffset Since, DateTimeOffset Until)
    : IQuery<IEnumerable<ScoreHighlightRecord>>;
