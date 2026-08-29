using MassTransit;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Messages;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The exit path for a stage break whose cause was never solved, or that a corrected note
///     count or a sharper rule has made wrong: re-solves every judged stage break, one player at
///     a time, from the same arithmetic the write path uses
///     (docs/design/pass-command-detection.md). Every judged stage break is re-derived rather
///     than only the unclassified ones — the solver is the moving part here, so a re-press is
///     what makes an improvement land. Touches nothing but the three cause columns; announces
///     nothing, because learning why an old run ended is not something a player did.
/// </summary>
internal sealed class BackfillStageBreakCausesConsumer(IScoreJournalRepository journal, IChartRepository charts,
        ILogger<BackfillStageBreakCausesConsumer> logger)
    : IConsumer<BackfillStageBreakCausesCommand>
{
    /// <summary>
    ///     Phoenix-family mixes only. A cause is read against a mix's grade floors and its life
    ///     bar, neither of which a legacy mix has.
    /// </summary>
    private static readonly MixEnum[] Mixes = Enum.GetValues<MixEnum>().Where(m => !m.UsesLegacyScoring()).ToArray();

    public async Task Consume(ConsumeContext<BackfillStageBreakCausesCommand> context)
    {
        var cancellationToken = context.CancellationToken;
        foreach (var mix in Mixes)
        {
            var users = await journal.GetUsersWithJudgedEntries(mix, cancellationToken);
            var rows = 0;
            var named = 0;
            foreach (var userId in users)
            {
                var (solved, withTarget) = await BackfillUser(mix, userId, cancellationToken);
                rows += solved;
                named += withTarget;
            }

            logger.LogInformation(
                "Stage break cause backfill on {Mix}: {Users} players, {Rows} stage breaks re-solved, {Named} naming a command",
                mix, users.Count, rows, named);
        }
    }

    private async Task<(int Rows, int Named)> BackfillUser(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var stageBreaks = (await journal.GetJudgedEntries(userId, mix, cancellationToken))
            .Where(e => e.IsStageBroken)
            .ToArray();
        if (stageBreaks.Length == 0) return (0, 0);

        // One catalog read per chart the player broke on. A player fails the same chart several
        // times in a row far more often than not, so this is well under one read per row.
        var facts = new Dictionary<Guid, ChartFacts>();
        foreach (var chartId in stageBreaks.Select(e => e.ChartId).Distinct())
            facts[chartId] = await NoteCountWatch.FactsFor(charts, mix, chartId, cancellationToken);

        var causes = stageBreaks
            .Select(e => (e.ChartId, e.OccurredAt,
                NoteCountWatch.CauseFor(true, e.Judgements, facts[e.ChartId], mix)))
            .ToArray();
        await journal.SetStageBreakCauses(userId, mix, causes, cancellationToken);

        return (causes.Length, causes.Count(c => c.Item3.IsNamed));
    }
}
