using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Projects Phoenix scores onto Phoenix 2: carried-over charts are rescored at their
///     P2 levels with the P2 formula (plate-priced, break-zeroing). Singles and Doubles are
///     each an int-floored top-50 pool; the overall total is ONE merged top-50 across both
///     types (NOT singles + doubles) — mirroring PlayerRatingSaga's P2 math — and the
///     [S]/[D] ladder titles come from the per-type pools.
/// </summary>
internal static class Phoenix2ProjectionCalculator
{
    public static RecapPhoenix2Projection? Calculate(IReadOnlyCollection<RecordedPhoenixScore> phoenixBests,
        IReadOnlyDictionary<Guid, Chart> phoenix2Charts)
    {
        var carried = phoenixBests
            .Where(b => b.Score != null && phoenix2Charts.ContainsKey(b.ChartId))
            .Select(b => (Record: b, Chart: phoenix2Charts[b.ChartId]))
            .Where(x => x.Chart.Type is ChartType.Single or ChartType.Double)
            .ToArray();
        if (carried.Length == 0) return null;

        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var singles = PoolTotal(ChartType.Single);
        var doubles = PoolTotal(ChartType.Double);
        // Overall PUMBILITY is ONE merged top-50 across both types (NOT singles + doubles) —
        // matching the live "All" board and PlayerRatingSaga.
        var total = (int)carried
            .Select(x => scoring.GetScore(x.Chart.Type, x.Chart.Level, x.Record.Score!.Value,
                x.Record.Plate ?? PhoenixPlate.RoughGame, x.Record.IsBroken))
            .OrderByDescending(r => r)
            .Take(50)
            .Sum();

        return new RecapPhoenix2Projection(
            singles,
            doubles,
            total,
            ProjectedTitle(PumbilityPool.Singles, singles),
            ProjectedTitle(PumbilityPool.Doubles, doubles),
            phoenixBests.Count(b => !b.IsBroken && phoenix2Charts.ContainsKey(b.ChartId)),
            phoenixBests.Count(b => !b.IsBroken));

        int PoolTotal(ChartType type)
        {
            return (int)carried
                .Where(x => x.Chart.Type == type)
                .Select(x => scoring.GetScore(x.Chart.Type, x.Chart.Level, x.Record.Score!.Value,
                    x.Record.Plate ?? PhoenixPlate.RoughGame, x.Record.IsBroken))
                .OrderByDescending(r => r)
                .Take(50)
                .Sum();
        }
    }

    public static string? ProjectedTitle(PumbilityPool pool, int pumbility)
    {
        return Phoenix2TitleList.BuildList()
            .OfType<Phoenix2PumbilityTitle>()
            .Where(t => t.Pool == pool && t.CompletionRequired <= pumbility)
            .OrderByDescending(t => t.CompletionRequired)
            .FirstOrDefault()?.Name.ToString();
    }
}
