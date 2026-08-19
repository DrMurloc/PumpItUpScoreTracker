using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The exit path for a max combo that was never stored, or that a corrected note count has
///     made wrong: re-solves every judged record and journal row, one player at a time, from the
///     same arithmetic the write path uses (docs/design/stage-breaks-and-max-combo.md D15).
///     Every judged row is re-derived rather than only the empty ones, so a re-press after a
///     catalog fix is what makes the fix land. Touches nothing but MaxCombo; announces nothing —
///     a combo appearing is not something a player did.
/// </summary>
internal sealed class BackfillMaxCombosConsumer(IPhoenixRecordRepository records, IScoreJournalRepository journal,
        IChartRepository charts, ILogger<BackfillMaxCombosConsumer> logger)
    : IConsumer<BackfillMaxCombosCommand>
{
    /// <summary>The mixes whose scores are Phoenix scores — the only ones a combo can be solved on.</summary>
    private static readonly MixEnum[] Mixes = Enum.GetValues<MixEnum>().Where(m => !m.UsesLegacyScoring()).ToArray();

    public async Task Consume(ConsumeContext<BackfillMaxCombosCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        foreach (var mix in Mixes)
        {
            var users = (await records.GetUsersWithJudgedRecords(mix, cancellationToken))
                .Concat(await journal.GetUsersWithJudgedEntries(mix, cancellationToken))
                .Distinct()
                .ToArray();
            var recordRows = 0;
            var journalRows = 0;
            foreach (var userId in users)
            {
                var (r, j) = await BackfillUser(mix, userId, cancellationToken);
                recordRows += r;
                journalRows += j;
            }

            logger.LogInformation(
                "Max combo backfill on {Mix}: {Users} players, {RecordRows} records and {JournalRows} journal rows re-solved",
                mix, users.Length, recordRows, journalRows);
        }
    }

    private async Task<(int Records, int JournalRows)> BackfillUser(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var judgedRecords = (await records.GetRecordedScores(mix, userId, cancellationToken))
            .Where(r => r.Judgements != null)
            .ToArray();
        var judgedEntries = await journal.GetJudgedEntries(userId, mix, cancellationToken);

        // One catalog read per chart the player has judged rows on, shared by both stores.
        var noteCounts = new Dictionary<Guid, int?>();
        foreach (var chartId in judgedRecords.Select(r => r.ChartId).Concat(judgedEntries.Select(e => e.ChartId))
                     .Distinct())
            noteCounts[chartId] = await NoteCountWatch.NoteCountFor(charts, mix, chartId, cancellationToken);

        await records.SetMaxCombos(mix, userId, judgedRecords
            .Select(r => (r.ChartId,
                PhoenixComboSolver.MaxComboFor(r.Judgements, r.Score, noteCounts[r.ChartId])))
            .ToArray(), cancellationToken);
        await journal.SetMaxCombos(userId, mix, judgedEntries
            .Select(e => (e.ChartId, e.OccurredAt,
                PhoenixComboSolver.MaxComboFor(e.Judgements, e.Score, noteCounts[e.ChartId])))
            .ToArray(), cancellationToken);

        return (judgedRecords.Length, judgedEntries.Count);
    }
}
