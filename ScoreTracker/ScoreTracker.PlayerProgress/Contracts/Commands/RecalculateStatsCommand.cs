using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Commands;

/// <summary>
///     ChangedChartIds + SessionId ride along when the recalculation follows a score
///     batch: they let the rating saga attribute competitive-level gains to the scores
///     that drove them (the CompetitiveImprover highlight flag). Admin-triggered
///     recalculations leave them null and no flags are written.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecalculateStatsCommand(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
    IReadOnlyList<Guid>? ChangedChartIds = null, Guid? SessionId = null) : IRequest
{
}
