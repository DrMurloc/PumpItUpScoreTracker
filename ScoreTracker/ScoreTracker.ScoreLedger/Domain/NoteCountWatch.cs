using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     The catalog's note count, as the two write paths need it: once to solve a play's max
///     combo, and once as a tripwire. A judged play whose sum disagrees with the stored count —
///     a pass, which judges every note, or a fail the site graded as finished — is written
///     exactly as it arrived and logs one warning naming the chart, so a stale catalog (a
///     re-step, a bad first sample) or the game's own edge (a graded fail that stopped a few
///     notes short) can be found in one query. It refuses nothing, reclassifies nothing and
///     rewrites nothing (docs/design/stage-breaks-and-max-combo.md D12-2, D13).
/// </summary>
internal static class NoteCountWatch
{
    /// <summary>
    ///     What the catalog knows about a chart that a written play needs: the note count, and
    ///     the level the stage-break solver sizes the life bar from. Both null when the chart is
    ///     not in the mix's catalog. One read, because the two arrive together.
    /// </summary>
    public static async Task<ChartFacts> FactsFor(IChartRepository charts, MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        try
        {
            var chart = await charts.GetChart(mix, chartId, cancellationToken);
            return chart == null ? default : new ChartFacts(chart.NoteCount, chart.Level, chart.Type);
        }
        catch (KeyNotFoundException)
        {
            return default;
        }
    }

    /// <summary>The stored count, or null when the chart is not in the mix's catalog.</summary>
    public static async Task<int?> NoteCountFor(IChartRepository charts, MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        return (await FactsFor(charts, mix, chartId, cancellationToken)).NoteCount;
    }

    /// <summary>
    ///     Solves a stage break's cause from the catalog's facts. Anything that is not a judged
    ///     stage break makes no claim — including a stage break the best list gave us with no
    ///     breakdown, which the recently-played card can still fill in later.
    /// </summary>
    public static StageBreakCause CauseFor(bool isStageBroken, JudgementCounts? judgements, ChartFacts facts,
        MixEnum mix)
    {
        // Co-op is never classified: its Level column is the PLAYER COUNT, so the life bar the
        // solver would size from it is fabricated (level 2 -> a 1,012 bar), and nothing is known
        // about how a co-op stage's life actually works. Never guess.
        if (!isStageBroken || judgements == null || facts.Type == ChartType.CoOp)
            return StageBreakCause.Unattributed;

        return StageBreakCauseSolver.Solve(judgements.Perfects, judgements.Greats, judgements.Goods,
            judgements.Bads, judgements.Misses, facts.NoteCount, facts.Level, mix);
    }

    /// <summary>
    ///     One warning when a finished play's breakdown does not sum to the catalog's count. A
    ///     stage break judged fewer notes by definition and is not a disagreement.
    /// </summary>
    public static void WarnOnDisagreement(ILogger logger, MixEnum mix, Guid chartId, JudgementCounts? judgements,
        int? noteCount, bool isBroken, bool isStageBroken)
    {
        if (isStageBroken || judgements == null || noteCount == null || judgements.NoteCount == noteCount) return;

        logger.LogWarning(
            "Note count disagreement on chart {ChartId} ({Mix}): the catalog says {NoteCount}, a {Outcome} judged {Judged}",
            chartId, mix, noteCount, isBroken ? "finished fail" : "pass", judgements.NoteCount);
    }
}

/// <summary>
///     The catalog facts a written play needs, read together because they come from one row.
///     Both null means the chart is not in this mix's catalog and nothing derived from it can
///     be claimed.
/// </summary>
internal readonly record struct ChartFacts(int? NoteCount, DifficultyLevel? Level, ChartType? Type = null);
