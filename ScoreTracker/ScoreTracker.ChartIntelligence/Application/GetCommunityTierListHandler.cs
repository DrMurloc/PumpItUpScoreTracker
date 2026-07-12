using MediatR;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

internal sealed class GetCommunityTierListHandler
    : IRequestHandler<GetCommunityTierListQuery, IEnumerable<SongTierListEntry>>
{
    public static readonly Name TierListName = "Community";

    private readonly IChartRepository _charts;
    private readonly IChartDifficultyRatingRepository _ratings;

    public GetCommunityTierListHandler(IChartDifficultyRatingRepository ratings, IChartRepository charts)
    {
        _ratings = ratings;
        _charts = charts;
    }

    public async Task<IEnumerable<SongTierListEntry>> Handle(GetCommunityTierListQuery request,
        CancellationToken cancellationToken)
    {
        var ratings = (await _ratings.GetAllChartRatedDifficulties(request.Mix, cancellationToken))
            .GroupBy(r => r.ChartId)
            .ToDictionary(g => g.Key, g => g.First());
        var charts = await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken);

        // Most-underrated first within the list; ties keep a stable name order so the
        // rendering doesn't shuffle between loads.
        return charts
            .Select(chart => (Chart: chart,
                Delta: ratings.TryGetValue(chart.Id, out var rating)
                    ? rating.Difficulty - ((int)chart.Level + .5)
                    : (double?)null))
            .OrderByDescending(x => x.Delta ?? double.MinValue)
            .ThenBy(x => x.Chart.Song.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select((x, index) => new SongTierListEntry(TierListName, x.Chart.Id, CategoryFor(x.Delta), index))
            .ToArray();
    }

    /// <summary>
    ///     Nearest-band mapping from the vote adjustment ladder (±.25, ±.5, ±1-level
    ///     steps — see DifficultyAdjustment) onto the tier list vocabulary. The two
    ///     extreme vote bands collapse into Overrated/Underrated, so legacy folders
    ///     read in exactly the same terms as Phoenix ones.
    /// </summary>
    private static TierListCategory CategoryFor(double? delta)
    {
        return delta switch
        {
            null => TierListCategory.Unrecorded,
            <= -.75 => TierListCategory.Overrated,
            <= -.375 => TierListCategory.VeryEasy,
            <= -.125 => TierListCategory.Easy,
            < .125 => TierListCategory.Medium,
            < .375 => TierListCategory.Hard,
            < .75 => TierListCategory.VeryHard,
            _ => TierListCategory.Underrated
        };
    }
}
