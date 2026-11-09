using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Domain.Models.Titles.Phoenix2;

/// <summary>
///     The three pools a mix's PUMBILITY ladders gate on, priced by the mix's own formula: [S] reads
///     the top 50 of the mix's Singles type, [D] the top 50 of its Doubles type, and the merged
///     [P.B] ladder the top 50 across both. Each contribution is Base × (grade + plate).
/// </summary>
public static class PumbilityLadderProgress
{
    /// <summary>The singles, doubles and merged pools of a player's records on the mix.</summary>
    public static (double Singles, double Doubles, double Total) Pools(MixEnum mix, IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, false);
        var singlesType = PumbilityPools.SinglesOn(mix);
        var doublesType = PumbilityPools.DoublesOn(mix);
        var contributions = attempts
            .Where(a => a.Score != null && charts.ContainsKey(a.ChartId))
            .Select(a => (charts[a.ChartId].Type, Value: scoring.GetScore(charts[a.ChartId].Type,
                charts[a.ChartId].Level, a.Score!.Value, a.Plate ?? PhoenixPlate.RoughGame, a.IsBroken)))
            .ToArray();
        // Unrounded: a ladder gates on the pool the player actually holds, and a rung sitting
        // inside the discarded fraction would read as unreached on one screen and earned on
        // another.
        var singles = contributions.Where(c => c.Type == singlesType)
            .Select(c => c.Value).OrderByDescending(v => v).Take(50).Sum();
        var doubles = contributions.Where(c => c.Type == doublesType)
            .Select(c => c.Value).OrderByDescending(v => v).Take(50).Sum();
        var total = contributions
            .Select(c => c.Value).OrderByDescending(v => v).Take(50).Sum();
        return (singles, doubles, total);
    }

    /// <summary>Sets every PUMBILITY rung's progress to the pool it gates on.</summary>
    public static void Apply(MixEnum mix, IEnumerable<PhoenixTitleProgress> progress, IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> attempts)
    {
        var (singles, doubles, total) = Pools(mix, charts, attempts);
        foreach (var title in progress)
            if (title.PhoenixTitle is Phoenix2PumbilityTitle pumbility)
                title.ApplyDirectProgress(pumbility.Pool switch
                {
                    PumbilityPool.Singles => singles,
                    PumbilityPool.Doubles => doubles,
                    _ => total
                });
    }
}
