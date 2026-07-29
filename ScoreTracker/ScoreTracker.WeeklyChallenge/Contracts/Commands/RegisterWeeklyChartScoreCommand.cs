using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Commands;

/// <summary>
///     Registers a score on the live weekly board. <paramref name="Source" /> defaults to
///     Manual — the self-report path (photos optional, proof-on-dispute); the official-import
///     consumer passes Official explicitly. The source describes the ranked score's
///     provenance: a submission that doesn't beat the existing score never demotes its tag.
///     <paramref name="Intent" /> defaults to BestWins, so an unqualified send keeps the
///     idempotent merge the importer depends on; the Record dialog passes Replace when a
///     player is correcting their own entry downward (weekly-charts-overhaul.md §9.2).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RegisterWeeklyChartScoreCommand(WeeklyTournamentEntry Entry, MixEnum Mix = MixEnum.Phoenix,
    ChallengeEntrySource Source = ChallengeEntrySource.Manual,
    WeeklyEntryIntent Intent = WeeklyEntryIntent.BestWins)
    : IRequest
{
}
