using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Commands;

/// <summary>
///     Registers a score on the live weekly board. <paramref name="Source" /> defaults to
///     Manual — the self-report path (photos optional, proof-on-dispute); the official-import
///     consumer passes Official explicitly. The source describes the ranked score's
///     provenance: a submission that doesn't beat the existing score never demotes its tag.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RegisterWeeklyChartScoreCommand(WeeklyTournamentEntry Entry, MixEnum Mix = MixEnum.Phoenix,
    ChallengeEntrySource Source = ChallengeEntrySource.Manual)
    : IRequest
{
}
