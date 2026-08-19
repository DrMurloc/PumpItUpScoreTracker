using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

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
    /// <summary>The stored count, or null when the chart is not in the mix's catalog.</summary>
    public static async Task<int?> NoteCountFor(IChartRepository charts, MixEnum mix, Guid chartId,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await charts.GetChart(mix, chartId, cancellationToken))?.NoteCount;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
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
