using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

public sealed class OfficialSiteClient : IOfficialSiteClient
{
    private readonly IPiuGameApi _piuGame;
    private readonly IChartRepository _charts;
    private readonly ILogger _logger;
    private readonly IMediator _mediator;
    private readonly ICurrentUserAccessor _currentUser;

    public OfficialSiteClient(IPiuGameApi piuGame, IChartRepository charts, ILogger<OfficialSiteClient> logger,
        IMediator mediator,
        ICurrentUserAccessor currentUser)
    {
        _piuGame = piuGame;
        _charts = charts;
        _logger = logger;
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<OfficialChartLeaderboardEntry>> GetAllOfficialChartScores(
        CancellationToken cancellationToken)
    {
        var songs = new List<PiuGameGetSongsResult.SongDto>();
        var page = 1;
        while (true)
        {
            var nextPage = await _piuGame.Get20AboveSongs(page, cancellationToken);
            songs.AddRange(nextPage.Results);
            if (nextPage.IsEnd) break;

            page++;
        }

        var current = 1;
        var max = songs.Count;
        var result = new List<OfficialChartLeaderboardEntry>();
        var misMatched = new List<string>();
        foreach (var song in songs)
        {
            var chartType = Enum.Parse<ChartType>(song.Type);
            var chart = (await _charts.GetChartsForSong(MixEnum.Phoenix, song.Name, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == song.Difficulty);
            if (chart == null)
            {
                misMatched.Add(song.Name + " " + song.Type + " " + song.Difficulty);
                continue;
            }

            _logger.LogInformation($"Song {current++} out of {max}");
            var scores = await _piuGame.GetSongLeaderboard(song.Id, cancellationToken);
            result.AddRange(scores.Results.Select(score =>
                new OfficialChartLeaderboardEntry(score.ProfileName, chart, score.Score)));
        }

        return result;
    }

    public async Task<IEnumerable<UserOfficialLeaderboard>> GetLeaderboardEntries(CancellationToken cancellationToken)
    {
        var leaderboardList = await _piuGame.GetLeaderboards(cancellationToken);
        var result = new List<UserOfficialLeaderboard>();
        foreach (var leaderboard in leaderboardList.Entries)
        {
            var entries = await _piuGame.GetLeaderboard(leaderboard.Id, cancellationToken);
            var place = 1;
            foreach (var entry in entries.Entries.OrderByDescending(e => e.Rating))
            {
                result.Add(new UserOfficialLeaderboard(entry.ProfileName, place, "Rating", leaderboard.Name,
                    entry.Rating));
                place++;
            }
        }

        return result;
    }

    public async Task<int> GetScorePageCount(string username, string password, CancellationToken cancellationToken)
    {
        var sessionId = await _piuGame.GetSessionId(username, password, cancellationToken);
        var response = await _piuGame.GetBestScores(sessionId, 0, cancellationToken);
        return response.MaxPage;
    }

    public async Task<IEnumerable<OfficialRecordedScore>> GetRecordedScores(string username, string password,
        int? maxPages, CancellationToken cancellationToken)
    {
        var currentPage = 1;
        await _mediator.Publish(new ImportStatusUpdated(_currentUser.User.Id, "Logging In"), cancellationToken);
        var sessionId = await _piuGame.GetSessionId(username, password, cancellationToken);
        var responses = new List<PiuGameGetBestScoresResult.ScoreDto>();
        maxPages ??= (await _piuGame.GetBestScores(sessionId, 1, cancellationToken)).MaxPage;
        while (currentPage <= maxPages.Value)
        {
            await _mediator.Publish(
                new ImportStatusUpdated(_currentUser.User.Id, $"Reading page {currentPage} of {maxPages}"),
                cancellationToken);
            var nextPage = await _piuGame.GetBestScores(sessionId, currentPage, cancellationToken);
            responses.AddRange(nextPage.Scores);
            if (nextPage.Scores.Length < 6)
                break;
            currentPage++;
            _logger.LogInformation($"Page {currentPage}");
        }

        var results = new List<OfficialRecordedScore>();
        foreach (var response in responses)
        {
            var chartType = response.ChartType;

            var song = response.SongName;
            if (ManualMappings.TryGetValue(song, out var mapping)) song = mapping;

            var chart = (await _charts.GetChartsForSong(MixEnum.Phoenix, song, cancellationToken))
                .FirstOrDefault(c => c.Type == chartType && c.Level == response.Level);
            if (chart == null) continue;

            results.Add(new OfficialRecordedScore(chart, response.Score, response.Plate));
        }

        return results;
    }

    private static readonly IDictionary<string, string> ManualMappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Kasou Shinja仮装信者", "Kasou Shinja" },
            { "Re：End of a Dream", "Re:End of a Dream" },
            { "CROSS RAY (feat. 月下Lia)", "Cross Ray" },
            { "ヨロピク ピクヨロ！", "Yoropiku Pikuyoro !" },
            { "甘い誘惑デインジャラス", "Amai Yuuwaku Dangerous" },
            { "甘い誘惑デインジャラス\nAmai Yuuwaku Dangerous", "Amai Yuuwaku Dangerous" },
            { "ヨロピク ピクヨロ！\nYoropiku Pikuyoro !", "Yoropiku Pikuyoro !" }
        };

    public async Task<IEnumerable<ChartPopularityLeaderboardEntry>> GetOfficialChartLeaderboardEntries(
        CancellationToken cancellationToken)
    {
        var missingCharts = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        var page = 0;
        var apiResults = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        while (true)
        {
            _logger.LogInformation($"Pulling page {page}");
            var nextResult = await _piuGame.GetChartPopularityLeaderboard(page, cancellationToken);
            apiResults.AddRange(nextResult.Entries);
            if (nextResult.Entries.Length < 50) break;

            page += 50;
        }

        var result = new List<ChartPopularityLeaderboardEntry>();
        foreach (var apiResult in apiResults)
        {
            var song = apiResult.SongName;
            if (ManualMappings.TryGetValue(song, out var mapping)) song = mapping;

            var charts = (await _charts.GetChartsForSong(MixEnum.Phoenix, song, cancellationToken)).ToArray();
            var chart = charts
                .FirstOrDefault(c => c.Level == apiResult.ChartLevel && c.Type == apiResult.ChartType);

            if (chart == null)
            {
                missingCharts.Add(apiResult);
                continue;
            }

            result.Add(new ChartPopularityLeaderboardEntry(chart, apiResult.Place));
        }

        var existing = result.Select(r => r.Chart.Id).Distinct().ToHashSet();
        var chartIds =
            (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
        var doesntExist = chartIds.Values.Where(c => !existing.Contains(c.Id)).ToArray();
        return result;
    }
}