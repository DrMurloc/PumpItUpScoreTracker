using System.Globalization;
using System.Net;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Contracts;
using ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Wiring;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis;

internal sealed class PiuGameApi : IPiuGameApi
{
    // Stay anchored to the two official hosts, but accept the optional "p2/" path segment —
    // the Phoenix 2 site serves stepballs from /l_img/p2/stepball/full/ (verified 2026-07-04).
    private static readonly Regex LevelRegex =
        new(@"^https\:\/\/(?:phoenix\.)?piugame\.com\/l_img\/(?:p2\/)?stepball\/full\/[a-zA-Z]_num_([0-9])\.png$", RegexOptions.Compiled);

    private static readonly Regex TypeRegex =
        new(@"^https\:\/\/(?:phoenix\.)?piugame\.com\/l_img\/(?:p2\/)?stepball\/full\/([a-zA-Z])_text\.png$", RegexOptions.Compiled);

    private static readonly Regex ShortTypeRegex =
        new(@"\/stepball\/full\/([a-zA-Z]+)_bg\.png", RegexOptions.Compiled);

    private static readonly Regex ShortLevelRegex =
        new(@"\/stepball\/full\/[a-zA-Z]+_num_([0-9])\.png", RegexOptions.Compiled);

    private static readonly Regex PlateRegex =
        new(@"\/plate\/([a-zA-Z]+)\.png", RegexOptions.Compiled);

    private static readonly Regex
        IdRegex = new(@"over_ranking_view\.php\?no=([a-zA-Z0-9]+)", RegexOptions.Compiled);

    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly PiuGameConfiguration _urls;

    public PiuGameApi(HttpClient client, ILogger<PiuGameApi> logger, ICurrentUserAccessor currentUser,
        IOptions<PiuGameConfiguration> configuration)
    {
        _client = client;
        _logger = logger;
        _currentUser = currentUser;
        _urls = configuration.Value;
    }

    public async Task<PiuGameGetSongsResult> Get20AboveSongs(MixEnum mix, int page,
        CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response = await GetWithRetries(
            $"{_urls.BaseUrlFor(mix)}/leaderboard/over_ranking.php?lv=&search=&&page={page}",
            cancellationToken, client);
        var document = new HtmlDocument();
        document.LoadHtml(response);
        var result = new List<PiuGameGetSongsResult.SongDto>();
        foreach (var songLi in document.DocumentNode.SelectNodes(
                     @"//ul[contains(@class, 'rating_ranking_list')]//div[contains(@class, 'li_in')]"))
        {
            if (songLi == null) continue;

            var linkUrl = songLi.SelectNodes(@".//a[contains(@class,'wrap')]").FirstOrDefault()
                ?.GetAttributeValue("href", "Unknown");

            var songName = songLi.SelectNodes(@".//div[contains(@class,'songName_w')]//p[contains(@class,'tt')]")
                .FirstOrDefault()?.InnerText.Trim() ?? "Unknown";
            var idMatch = IdRegex.Match(linkUrl);
            var id = idMatch.Success ? idMatch.Groups[1].Value : "Unknown";
            var chartTypeUrl = songLi
                .SelectNodes(@".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
                .FirstOrDefault()?.GetAttributeValue("src", "Unknown");

            var chartType = GetChartTypeFromUrl(chartTypeUrl);
            var difficultyLevelUrls = songLi
                .SelectNodes(@".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'numw')]//img")
                .Select(i => i.GetAttributeValue("src", "Unknown")).ToArray();
            var level = 0;
            foreach (var url in difficultyLevelUrls)
            {
                level *= 10;
                var match = LevelRegex.Match(url);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsedLevel)) continue;

                level += parsedLevel;
            }

            if (level == 0) level = 29;
            songName = HttpUtility.HtmlDecode(songName);
            if (songName.Contains("End of a Dream"))
                songName = "Re:End of a Dream";
            else if (songName.Contains("CROSS RAY"))
                songName = "Cross Ray";
            else if (songName.Contains("Kasou Shinja") &&
                     !songName.Contains("SHORT", StringComparison.OrdinalIgnoreCase))
                songName = "Kasou Shinja";
            else if (songName.Contains("Yoropiku Pikuyoro")) songName = "Yoropiku Pikuyoro !";
            result.Add(new PiuGameGetSongsResult.SongDto
            {
                Difficulty = level,
                Id = id,
                Name = songName,
                Type = chartType.ToString()
            });
        }

        var nextIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'next')]");
        var lastIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'last')]");
        return new PiuGameGetSongsResult
        {
            Results = result.ToArray(),
            IsEnd = (nextIcon == null || !nextIcon.Any()) && (lastIcon == null || !lastIcon.Any())
        };
    }


    public async Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(MixEnum mix, string songId, int page,
        CancellationToken cancellationToken, HttpClient? client = null)
    {
        var response =
            await GetWithRetries($"{_urls.BaseUrlFor(mix)}/leaderboard/over_ranking_view.php?no={songId}&page={page}",
                cancellationToken, client);
        var document = new HtmlDocument();
        document.LoadHtml(response);
        var results = new List<PiuGameGetSongLeaderboardResult.EntryResultDto>();
        var failedRows = 0;
        var lis = document.DocumentNode.SelectNodes("//div[contains(@class,'rangking_list_w')]//li");
        if (lis != null)
            foreach (var li in lis)
            {
                var scoreNode = li.SelectSingleNode(".//div[contains(@class,'score')]//i[contains(@class,'tt')]");
                var profileName = string.Join("", li.SelectNodes(".//div[contains(@class,'profile_name')]")
                    .Select(n => n.InnerText));
                var avatarNode =
                    li.SelectSingleNode(".//div[contains(@class,'profile_img')]//div[contains(@class,'bgfix')]");
                try
                {
                    results.Add(new PiuGameGetSongLeaderboardResult.EntryResultDto
                    {
                        Score = int.Parse(scoreNode.InnerText, NumberStyles.AllowThousands, CultureInfo.InvariantCulture),
                        ProfileName = profileName,
                        AvatarUrl = new Uri(ImageRegex.Match(avatarNode.GetAttributeValue("style", "")).Groups[1].Value)
                    });
                }
                catch (Exception)
                {
                    failedRows++;
                }
            }

        // A row that fails to parse is a dropped player, not noise — count it so board
        // skips and site drift are visible in the run log instead of silently shrinking
        // boards.
        if (failedRows > 0)
            _logger.LogWarning("Board {SongId} page {Page}: {Failed} of {Total} rows failed to parse", songId,
                page, failedRows, lis?.Count ?? 0);

        var nextIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'next')]");
        var lastIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'last')]");
        return new PiuGameGetSongLeaderboardResult
        {
            Results = results.ToArray(),
            FailedRows = failedRows,
            IsEnd = (nextIcon == null || !nextIcon.Any()) && (lastIcon == null || !lastIcon.Any())
        };
    }

    public async Task<PiuGameGetPumbilityRankingResult> GetPumbilityRankings(MixEnum mix, ChartType? chartType,
        int page, HttpClient? client, CancellationToken cancellationToken)
    {
        var tab = chartType switch
        {
            null => "",
            ChartType.Single => "s",
            ChartType.Double => "d",
            _ => throw new ArgumentOutOfRangeException(nameof(chartType), chartType,
                "The PUMBILITY ranking only has All/Single/Double tabs")
        };
        var response = await GetWithRetries(
            $"{_urls.BaseUrlFor(mix)}/leaderboard/pumbility_ranking.php?t={tab}&page={page}",
            cancellationToken, client);
        var document = new HtmlDocument();
        document.LoadHtml(response);

        // The page renders the viewer's own "MY RANKING DATA" block with the same list
        // markup as the ranking itself — exclude it or the service account leaks into
        // every page of results.
        var lis = document.DocumentNode.SelectNodes(
            "//ul[contains(@class,'pumbilitySt') and not(ancestor::div[contains(@class,'my_pumblitiy_wrap')])]/li");
        var entries = new List<PiuGameGetPumbilityRankingResult.Entry>();
        if (lis != null)
            foreach (var li in lis)
                try
                {
                    var profileName = string.Join("", li.SelectNodes(".//div[contains(@class,'profile_name')]")
                        .Select(n => n.InnerText.Trim()));
                    var title = li.SelectSingleNode(".//div[contains(@class,'profile_title')]")?.InnerText.Trim()
                                ?? string.Empty;
                    // The value renders as "17,418<span class=pumbility-point-sub>.45</span>" —
                    // InnerText flattens to "17,418.45". Invariant parse: PIU always uses ","
                    // thousands / "." decimals regardless of the request thread's culture.
                    var scoreText = li.SelectSingleNode(".//div[contains(@class,'score')]//i[contains(@class,'tt')]")
                        .InnerText.Replace(",", "").Trim();
                    var avatarNode =
                        li.SelectSingleNode(".//div[contains(@class,'profile_img')]//div[contains(@class,'bgfix')]");
                    var avatarMatch = ImageRegex.Match(avatarNode?.GetAttributeValue("style", "") ?? "");
                    entries.Add(new PiuGameGetPumbilityRankingResult.Entry
                    {
                        ProfileName = HttpUtility.HtmlDecode(profileName),
                        Title = HttpUtility.HtmlDecode(title),
                        Pumbility = double.Parse(scoreText, NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture),
                        AvatarUrl = avatarMatch.Success ? new Uri(avatarMatch.Groups[1].Value) : null
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error parsing a PUMBILITY ranking row on page {Page}", page);
                }

        var nextIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'next')]");
        var lastIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'last')]");
        return new PiuGameGetPumbilityRankingResult
        {
            Entries = entries.ToArray(),
            IsEnd = (nextIcon == null || !nextIcon.Any()) && (lastIcon == null || !lastIcon.Any())
        };
    }

    public async Task<PiuGameGetLeaderboardListResult> GetLeaderboards(MixEnum mix, CancellationToken cancellationToken)
    {
        var response =
            await GetWithRetries($"{_urls.BaseUrlFor(mix)}/leaderboard/rating_ranking.php", cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(response);

        var results = new List<PiuGameGetLeaderboardListResult.Entry>();
        var options = document.DocumentNode.SelectNodes(".//div[contains(@class,'search')]//option");
        if (options != null)
            results.AddRange(options.Select(option => new PiuGameGetLeaderboardListResult.Entry
                { Id = option.GetAttributeValue("value", ""), Name = option.InnerText }));
        else throw new MalformedLineException("Missing options to search for leaderboards");


        return new PiuGameGetLeaderboardListResult
        {
            Entries = results.ToArray()
        };
    }

    public async Task<PiuGameGetLeaderboardResult> GetLeaderboard(MixEnum mix, string leaderboardId,
        CancellationToken cancellationToken)
    {
        var response =
            await GetWithRetries($"{_urls.BaseUrlFor(mix)}/leaderboard/rating_ranking.php?lv=" + leaderboardId,
                cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(response);
        var lis = document.DocumentNode.SelectNodes(".//div[contains(@class,'rating_ranking_wrap')]//li");
        if (lis == null)
            throw new MalformedLineException($"Couldn't get lis from {leaderboardId} leaderboard");

        var results = new List<PiuGameGetLeaderboardResult.Entry>();
        foreach (var li in lis)
        {
            var userName = string.Join("",
                li.SelectNodes(".//div[contains(@class,'profile_name')]").Select(n => n.InnerText));
            var rating = int.Parse(li.SelectSingleNode(".//div[contains(@class,'score')]/i").InnerText,
                NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
            results.Add(new PiuGameGetLeaderboardResult.Entry
            {
                ProfileName = userName,
                Rating = rating
            });
        }

        return new PiuGameGetLeaderboardResult
        {
            Entries = results.ToArray()
        };
    }

    public async Task<PiuGameGetChartPopularityLeaderboardResult> GetChartPopularityLeaderboard(MixEnum mix, int page,
        DateTimeOffset asOf, CancellationToken cancellationToken, HttpClient? client = null)
    {
        var target = asOf - TimeSpan.FromDays(1);
        var response = await PostWithRetries($"{_urls.BaseUrlFor(mix)}/ajax/top_steps.php",
            new Dictionary<string, string>
            {
                { "page", page.ToString() },
                // Zero-padded month is mandatory: the endpoint answers "20267" with a redirect
                // script pointing at "202607" and no data.
                { "date", $"{target.Year}{target.Month:00}" },
                { "mode", "full" }
            }, cancellationToken, client);
        var results = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        var document = new HtmlDocument();
        document.LoadHtml(response);
        var lis = document.DocumentNode.SelectNodes("./li");
        if (lis == null)
            return new PiuGameGetChartPopularityLeaderboardResult
            {
                Entries = results.ToArray(),
                RawRowCount = 0
            };

        foreach (var li in lis)
        {
            var placeIcon = li.SelectSingleNode(".//div[contains(@class,'num')]/i[contains(@class,'tt')]");
            int place;
            if (placeIcon == null)
            {
                var medal = li.SelectSingleNode(".//span[contains(@class,'medal_wrap')]//img");
                if (medal == null) continue;

                switch (medal.GetAttributeValue("src", "/").ToLower().Split("/")[^1])
                {
                    case "goldmedal.png":
                        place = 1;
                        break;
                    case "silvermedal.png":
                        place = 2;
                        break;
                    case "bronzemedal.png":
                        place = 3;
                        break;
                    default:
                        continue;
                }
            }
            else
            {
                place = int.Parse(placeIcon.InnerText);
            }

            var songImage = li.SelectSingleNode(".//div[contains(@class,'bgfix')]");

            var scoreP = li.SelectSingleNode(".//div[contains(@class,'profile_name')]/p[contains(@class,'t1')]");
            if (scoreP == null) continue;

            var difficultyLevelUrls = li
                .SelectNodes(@".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'numw')]//img")
                .Select(i => i.GetAttributeValue("src", "Unknown")).ToArray();
            var level = 0;
            foreach (var url in difficultyLevelUrls)
            {
                level *= 10;
                var match = LevelRegex.Match(url);

                if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsedLevel)) continue;

                level += parsedLevel;
            }

            var songName = HttpUtility.HtmlDecode(scoreP.InnerText);
            if (level == 0)
            {
                if (songName == "1948")
                    level = 29;
                else
                    continue;
            }

            var chartTypeUrl = li
                .SelectNodes(@".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
                .FirstOrDefault()?.GetAttributeValue("src", "Unknown");
            if (chartTypeUrl == null) continue;
            var chartType = GetChartTypeFromUrl(chartTypeUrl);
            if (chartType == null) continue;
            var image = ImageRegex.Match(songImage.GetAttributeValue("style", "")).Groups[1].Value;
            results.Add(new PiuGameGetChartPopularityLeaderboardResult.Entry
            {
                ChartLevel = level,
                ChartType = chartType!.Value,
                Place = place,
                SongName = HttpUtility.HtmlDecode(scoreP.InnerText),
                SongImage = image
            });
        }

        return new PiuGameGetChartPopularityLeaderboardResult
        {
            Entries = results.ToArray(),
            RawRowCount = lis.Count
        };
    }

    public async Task<IEnumerable<PiuGameGetRecentScoresResult>> GetRecentScores(MixEnum mix, HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries($"{_urls.BaseUrlFor(mix)}/my_page/recently_played.php",
            cancellationToken, client);

        var document = new HtmlDocument();
        document.LoadHtml(response);
        var cards = document.DocumentNode.SelectNodes(
            ".//ul[contains(@class,'recently_playeList')]/li");
        if (cards == null) return Array.Empty<PiuGameGetRecentScoresResult>();
        var results = new List<PiuGameGetRecentScoresResult>();
        foreach (var card in cards)
            try
            {
                if (card.SelectNodes(".//div[contains(@class,'li_in')]/i[contains(@class,'tx')]")
                        ?.Any(n => n.InnerText == "STAGE BREAK") ?? false)
                    continue;
                var isBroken = !(card.SelectNodes(".//div[contains(@class,'li_in')]/img")
                    ?.Any(n => n.GetAttributeValue("src", "").Contains("/plate/")) ?? false);

                var score = int.Parse(card
                                          .SelectSingleNode(".//div[contains(@class,'li_in')]/i[contains(@class,'tx')]")
                                          ?.InnerText.Replace(",", "") ??
                                      "",
                    NumberStyles.AllowThousands);
                var songName =
                    HttpUtility.HtmlDecode(card.SelectSingleNode(".//div[contains(@class,'song_name')]/p").InnerText);
                var chartTypeUrl = card
                    .SelectNodes(@".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
                    .FirstOrDefault()?.GetAttributeValue("src", "Unknown");

                var chartType = GetChartTypeFromUrl(chartTypeUrl);
                if (chartType == null) continue;
                var difficultyLevelUrls = card
                    .SelectNodes(@".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'numw')]//img")
                    .Select(i => i.GetAttributeValue("src", "Unknown")).ToArray();
                var level = 0;
                foreach (var url in difficultyLevelUrls)
                {
                    level *= 10;
                    var match = LevelRegex.Match(url);

                    if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsedLevel)) continue;

                    level += parsedLevel;
                }

                if (level == 0) level = 29;
                // PIU formats note counts with "," as thousand separator (e.g. "1,414"). Without an
                // explicit CultureInfo, int.Parse uses the request thread's current culture, which
                // ASP.NET Core's RequestLocalizationMiddleware sets per-user. For users browsing in
                // pt-BR / fr-FR / it-IT etc. (where "," is the decimal separator), parsing throws
                // FormatException and the score entry is silently dropped by the try/catch below.
                // Force InvariantCulture so PIU's format is parsed the same way for every request.
                var perfects = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'PERFECT')]/div").InnerText,
                    NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                var greats = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'GREAT')]/div").InnerText,
                    NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                var goods = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'GOOD')]/div").InnerText,
                    NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                var bads = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'BAD')]/div").InnerText,
                    NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                var misses = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'MISS')]/div").InnerText,
                    NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
                // The recent-play card shows no plate image, so a passed play's plate is
                // derived from its judgement counts. A broken play gets none: the game awards
                // no plate for a failed stage, and PlateText reads all-zero counts (a walk-off)
                // as a Perfect Game.
                var plate = isBroken
                    ? (PhoenixPlate?)null
                    : new ScoreScreen(perfects, greats, goods, bads, misses, 0).PlateText;
                results.Add(new PiuGameGetRecentScoresResult
                {
                    ChartType = chartType!.Value,
                    Level = level,
                    NoteCount = perfects + greats + goods + bads + misses,
                    Plate = plate,
                    Grade = GradeFrom(card.InnerHtml),
                    SongName = songName,
                    IsBroken = isBroken,
                    Score = score,
                    Perfects = perfects,
                    Greats = greats,
                    Goods = goods,
                    Bads = bads,
                    Misses = misses,
                    RecordedAt = ParseRecordedAt(card)
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    _currentUser.IsLoggedIn
                        ? $"Error parsing recent score for {_currentUser.User.Id} {_currentUser.User.Name} - {_currentUser.User.GameTag}"
                        : "Error parsing recent scores");
            }

        return results;
    }

    private Task<string> PostWithRetries(string url, IDictionary<string, string> form,
        CancellationToken cancellationToken = default, HttpClient? client = null)
    {
        return WithRetries(async () =>
        {
            var response = await (client ?? _client).PostAsync(url, new FormUrlEncodedContent(form),
                cancellationToken);
            ThrowIfSsoBounced(response);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }, url, cancellationToken);
    }

    private Task<HttpResponseMessage> PostForMessageWithRetries(string url, IDictionary<string, string> form,
        CancellationToken cancellationToken = default, HttpClient? client = null)
    {
        return WithRetries(async () =>
        {
            var response =
                await (client ?? _client).PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);

            ThrowIfSsoBounced(response);
            //response.EnsureSuccessStatusCode();
            return response;
        }, url, cancellationToken);
    }

    private Task<string> GetWithRetries(string url, CancellationToken cancellationToken = default,
        HttpClient? client = null)
    {
        return WithRetries(async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await (client ?? _client).SendAsync(request, cancellationToken);
            ThrowIfSsoBounced(response);
            // An error page must fail the fetch, not parse as an empty board.
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }, url, cancellationToken);
    }

    /// <summary>
    ///     Every call into the official site goes through here. The site's SSO bounce makes the
    ///     FIRST request of a fresh session fail by design (see
    ///     <see cref="ThrowIfSsoBounced" />), and its edge intermittently resets connections
    ///     mid-TLS-handshake under sweep load — so a single attempt is never a verdict. Backoff
    ///     grows between tries: a flat second isn't long enough for an edge that just dropped us,
    ///     and the weekly sweep can afford seven seconds far more than it can afford a lost week.
    ///     A cancelled run is a decision, not a fault, and stops here — but only when it is OUR
    ///     token that fired: HttpClient reports its own request timeout as a cancellation too, and
    ///     that one is exactly the transient we are here to survive.
    /// </summary>
    private async Task<T> WithRetries<T>(Func<Task<T>> attempt, string url, CancellationToken cancellationToken)
    {
        for (var retry = 0;; retry++)
            try
            {
                return await attempt();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (retry < MaxRetries)
            {
                var delay = _urls.RetryBaseDelayMilliseconds * (1 << retry);
                // The SSO bounce is the site working as designed, so it stays out of the warning
                // stream — otherwise every fresh session cries wolf and buries the real resets.
                if (e is SsoBounceException)
                    _logger.LogDebug("{Url} bounced through SSO; retrying with session cookies", url);
                else
                    _logger.LogWarning(e, "{Url} failed (attempt {Attempt} of {Total}); retrying in {Delay}ms", url,
                        retry + 1, MaxRetries + 1, delay);
                if (delay > 0) await Task.Delay(delay, cancellationToken);
            }
    }

    private const int MaxRetries = 3;

    /// <summary>
    ///     Since the Phoenix 2 site rollout, phoenix.piugame.com fronts anonymous traffic with an
    ///     am-pass SSO handshake: a fresh session's first request redirects through am-pass.net
    ///     and dead-ends on a broken /ssoc URL (their bug — a browser recovers, HttpClient
    ///     doesn't). The hop still deposits the anonymous session cookies in this client's
    ///     cookie container, so an immediate retry of the original URL succeeds. Landing on
    ///     /ssoc is therefore a transient failure for the retry loops above, not a result.
    /// </summary>
    private static void ThrowIfSsoBounced(HttpResponseMessage response)
    {
        if (response.RequestMessage?.RequestUri?.AbsolutePath.Contains("ssoc", StringComparison.OrdinalIgnoreCase) ==
            true)
            throw new SsoBounceException(
                $"Bounced to the am-pass SSO handshake ({response.RequestMessage.RequestUri}); retrying now that session cookies are set.");
    }

    /// <summary>The expected first-request-of-a-session bounce, told apart from a real fault.</summary>
    private sealed class SsoBounceException : HttpRequestException
    {
        public SsoBounceException(string message) : base(message)
        {
        }
    }

    private ChartType? GetChartTypeFromUrl(string chartTypeUrl)
    {
        var typeMatch = TypeRegex.Match(chartTypeUrl);
        return typeMatch.Success
            ? typeMatch.Groups[1].Value.ToLower() switch
            {
                "c" => ChartType.CoOp,
                "s" => ChartType.Single,
                "d" => ChartType.Double,
                "sp" => ChartType.SinglePerformance,
                "dp" => ChartType.DoublePerformance,
                _ => null
            }
            : ChartType.SinglePerformance;
    }

    public async Task<(HttpClient client, string sid)> GetSessionId(MixEnum mix, string username, string password,
        CancellationToken cancellationToken)
    {
        var baseUrl = _urls.BaseUrlFor(mix);
        var cookieContainer = new CookieContainer();
        var webRequestHandler = new HttpClientHandler
        {
            CookieContainer = cookieContainer
        };
        var client = new HttpClient(webRequestHandler);
        client.DefaultRequestHeaders.Add("origin", baseUrl);

        // The warm-up hop that collects the site's anonymous session cookies, and the am-pass
        // hop below, run through the same retry policy as everything else. They used to be bare
        // awaits, which made them the one un-retried pair of requests in the whole client — and
        // the weekly Phoenix 2 sweep opens with them, so a single reset TLS handshake here
        // (2026-07-26) killed a run that the retry policy would have absorbed anywhere else.
        // Phoenix 1's sweep needs no login at all, which is why it sailed through the same window.
        await WithRetries(() => client.GetAsync(baseUrl, cancellationToken), baseUrl, cancellationToken);

        var result = await PostForMessageWithRetries($"{baseUrl}/bbs/login_check.php",
            new Dictionary<string, string>
            {
                { "url", "/" },
                { "mb_id", username },
                { "mb_password", password }
            }, cancellationToken, client);

        // No session cookie after the login POST means the site rejected the login outright —
        // credentials, not profile state. (The usual wrong-password shape still deposits an
        // anonymous sid and surfaces later as GetAccountData's login-page INVALID.)
        var sid = cookieContainer
            .GetCookies(new Uri(baseUrl))
            .FirstOrDefault(v => v.Name.StartsWith("sid", StringComparison.OrdinalIgnoreCase))?.Value;
        if (sid == null) throw new InvalidCredentialException("Could not log in user to PIUgame");
        await WithRetries(() => client.GetAsync(_urls.AmPassUrl, cancellationToken), _urls.AmPassUrl,
            cancellationToken);
        return (client, sid);
    }

    public HttpClient ClientForSid(MixEnum mix, string sid)
    {
        var baseUrl = _urls.BaseUrlFor(mix);
        var cookieContainer = new CookieContainer();
        cookieContainer.Add(new Uri(baseUrl), new Cookie("sid", sid));
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookieContainer });
        client.DefaultRequestHeaders.Add("origin", baseUrl);
        return client;
    }

    public async Task<PiuGameGetBestScoresResult> GetBestScores(MixEnum mix, HttpClient client, int page,
        CancellationToken cancellationToken, string? bucket = null)
    {
        var filter = string.IsNullOrEmpty(bucket) ? "" : $"lv={HttpUtility.UrlEncode(bucket)}";
        var response = await GetWithRetries(
            $"{_urls.BaseUrlFor(mix)}/my_page/my_best_score.php?{filter}&&page={page}",
            cancellationToken, client);

        var document = new HtmlDocument();
        document.LoadHtml(response);

        // Two page generations share this URL: the classic my_best_scoreList layout, and the
        // Phoenix 2 redesign that reuses the recently-played card layout with a saved date on
        // every best. Sniff the markup instead of keying on mix, so the redesign reaching the
        // other host changes nothing here.
        var result = document.DocumentNode.SelectSingleNode(".//ul[contains(@class,'my_best_scoreList')]") != null
            ? ParseClassicBestScores(document, page)
            : ParseRedesignedBestScores(document, page);
        result.TotalCharts = ParseTotalCharts(document);
        return result;
    }

    /// <summary>
    ///     The list's "Total." header. It is the only place the site states how many charts an
    ///     account holds BELOW level 10, since neither page's level filter offers a bucket for
    ///     them — the completeness census recovers those as a residual against this number.
    /// </summary>
    private static int? ParseTotalCharts(HtmlDocument document)
    {
        var wrap = document.DocumentNode.SelectSingleNode("//*[contains(@class,'total_wrap')]");
        if (wrap == null) return null;
        var match = CountRegex.Match(HttpUtility.HtmlDecode(wrap.InnerText));
        return match.Success ? int.Parse(match.Value.Replace(",", ""), CultureInfo.InvariantCulture) : null;
    }

    private static int ParseBestScoresMaxPage(HtmlDocument document, int page)
    {
        var lastI = document.DocumentNode.SelectNodes(".//i[contains(@class,'last')]")?.First();
        var maxPageStrings = lastI?.ParentNode
            .GetAttributeValue("onclick", "")
            .Split("=") ?? Array.Empty<string>();
        return maxPageStrings.Length > 0 ? int.Parse(maxPageStrings[^1].TrimEnd('\'') ?? "") : page;
    }

    // "2026-07-17 23:16:30 (GMT+9)" — my_page timestamps carry their UTC offset inline.
    private static readonly Regex RecordedAtRegex =
        new(@"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\s*\(GMT([+-]\d{1,2})\)", RegexOptions.Compiled);

    private static DateTimeOffset? ParseRecordedAt(HtmlNode card)
    {
        var text = card.SelectSingleNode(".//p[contains(@class,'recently_date_tt')]")?.InnerText;
        if (text == null) return null;

        var match = RecordedAtRegex.Match(text);
        if (!match.Success) return null;

        var local = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None);
        return new DateTimeOffset(local,
            TimeSpan.FromHours(int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)));
    }

    private PiuGameGetBestScoresResult ParseRedesignedBestScores(HtmlDocument document, int page)
    {
        var result = new PiuGameGetBestScoresResult
        {
            MaxPage = ParseBestScoresMaxPage(document, page)
        };
        var cards = document.DocumentNode.SelectNodes(".//ul[contains(@class,'recently_playeList')]/li");
        if (cards == null) return result;

        var scores = new List<PiuGameGetBestScoresResult.ScoreDto>();
        foreach (var card in cards)
            try
            {
                var typeUrl = card
                    .SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
                    .First().GetAttributeValue("src", "");
                if (typeUrl.Contains("u_text", StringComparison.OrdinalIgnoreCase))
                    //UCS
                    continue;

                var chartType = GetChartTypeFromUrl(typeUrl);
                if (chartType == null) continue;

                var level = 0;
                foreach (var url in card
                             .SelectNodes(
                                 ".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'numw')]//img")
                             .Select(i => i.GetAttributeValue("src", "Unknown")))
                {
                    level *= 10;
                    var match = LevelRegex.Match(url);
                    if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsedLevel)) continue;

                    level += parsedLevel;
                }

                if (level == 0) level = 29;

                var score = int.Parse(
                    card.SelectSingleNode(
                            ".//div[contains(@class,'li_in') and contains(@class,'ac')]/i[contains(@class,'tx')]")
                        .InnerText.Replace(",", ""),
                    NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

                // The plate renders in its own li_in beside the score's (which holds the grade
                // image); no plate image anywhere on the card means a broken (stage-failed)
                // best — the redesign lists those too, usually with a real partial score.
                var plateMatch = card
                    .SelectNodes(".//div[contains(@class,'li_in')]/img")
                    ?.Select(i => PlateRegex.Match(i.GetAttributeValue("src", "")))
                    .FirstOrDefault(m => m.Success);

                scores.Add(new PiuGameGetBestScoresResult.ScoreDto
                {
                    SongName = HttpUtility.HtmlDecode(
                        card.SelectSingleNode(".//div[contains(@class,'song_name')]/p").InnerText),
                    ChartType = chartType.Value,
                    Level = level,
                    Score = score,
                    Plate = plateMatch == null
                        ? null
                        : PhoenixPlateHelperMethods.ParseShorthand(plateMatch.Groups[1].Value),
                    IsBroken = plateMatch == null,
                    RecordedAt = ParseRecordedAt(card)
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing a redesigned best-score card");
            }

        result.Scores = scores.ToArray();
        return result;
    }

    private PiuGameGetBestScoresResult ParseClassicBestScores(HtmlDocument document, int page)
    {
        var result = new PiuGameGetBestScoresResult
        {
            MaxPage = ParseBestScoresMaxPage(document, page)
        };

        var foundScores =
            document.DocumentNode.SelectNodes(
                ".//ul[contains(@class,'my_best_scoreList')]/li/div[contains(@class,'in')]");
        if (foundScores == null) return result;
        var scores = new List<PiuGameGetBestScoresResult.ScoreDto>();
        foreach (var scoreCard in foundScores)
        {
            var songName = HttpUtility.HtmlDecode(scoreCard.SelectNodes(".//div[contains(@class,'song_name')]").First()
                .ChildNodes.First()
                .InnerText);
            var typeString = scoreCard
                .SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
                .First().GetAttributeValue("src", "");
            if (typeString.Contains("u_text", StringComparison.OrdinalIgnoreCase))
                //UCS
                continue;
            var chartType = GetChartTypeFromUrl(typeString);

            // Match the digit out of each level-image filename instead of a fixed character
            // offset — the offset broke on 2026-07-03 when PIU moved these images from
            // piugame.com to phoenix.piugame.com (+8 chars) with the Phoenix 2 site rollout.
            var difficultyDigits = string.Join("",
                scoreCard.SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'imG')]//img")
                    .Select(n => ShortLevelRegex.Match(n.GetAttributeValue("src", "")))
                    .Where(m => m.Success)
                    .Select(m => m.Groups[1].Value));
            // 1948 D29 renders a "??" stepball on the Phoenix 2 site — no parseable digit
            // images — so the joined digits come back empty (int.Parse used to throw and the
            // per-score catch silently dropped the card). Owner-confirmed: ?? is functionally a 29.
            var difficulty = difficultyDigits.Length == 0 ? 29 : int.Parse(difficultyDigits);

            var scoreList = scoreCard.SelectNodes(".//div[contains(@class,'etc_con')]//ul").First();

            var score = scoreList.ChildNodes[1].ChildNodes[1].ChildNodes[1].ChildNodes[0].InnerText.Trim()
                .Replace(",", "");
            var plate = PlateRegex.Match(scoreList.ChildNodes[5].ChildNodes[1].ChildNodes[1].ChildNodes[0]
                .GetAttributeValue("src", "")).Groups[1].Value;
            try
            {
                scores.Add(new PiuGameGetBestScoresResult.ScoreDto
                {
                    ChartType = chartType!.Value,
                    Level = difficulty,
                    Plate = PhoenixPlateHelperMethods.ParseShorthand(plate),
                    Score = int.Parse(score),
                    SongName = songName
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing best score for {SongName}", songName);
            }
        }

        result.Scores = scores.ToArray();
        return result;
    }

    // A count tile renders "129" on Phoenix and "2 / 4,476" on Phoenix 2 — the first number is
    // the count, the second (when present) the mix's chart total for the bucket.
    // Every pattern below runs against a whole page of markup we do not control, so each carries a
    // match timeout: a page that changes shape must fail this parse, not hang the thread on
    // backtracking.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    private static readonly Regex CountRegex = new(@"\d[\d,]*", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex DecimalRegex =
        new(@"\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled, RegexTimeout);

    // "354<span class="pumbility-point-sub">.24</span>" — the lazy .*? spans arbitrary markup
    // between the class and the value, which is exactly the shape that backtracks badly.
    private static readonly Regex PumbilityValueRegex = new(
        @"(?s)class=""in score"".*?>\s*([0-9,]+)<span class=""pumbility-point-sub"">\.(\d+)</span>",
        RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex PageNumberRegex = new(@"page=(\d+)", RegexOptions.Compiled, RegexTimeout);

    private static readonly Regex GradeImageRegex =
        new(@"\/grade\/([a-z_]+)\.png", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

    // The breakdown page prefixes its plate art "s_" (s_mg.png) where the rest of the site serves
    // it bare, so this accepts either.
    private static readonly Regex PlateImageRegex =
        new(@"\/plate\/(?:s_)?([a-zA-Z]+)\.png", RegexOptions.Compiled, RegexTimeout);

    /// <summary>
    ///     One bucket of <c>my_page/play_data.php</c> — the cheapest complete statement the site
    ///     makes about an account, since it counts every PASS at a level in a single request.
    ///     Phoenix 2's grade and plate tiles are cumulative and Phoenix's plate tiles are exact;
    ///     both leave out a band the player has none of. This normalises the pair to exact counts
    ///     so nothing downstream has to know which mix it is reading.
    /// </summary>
    public async Task<PiuGameGetPlayDataResult> GetPlayData(MixEnum mix, HttpClient client, string bucket,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries(
            $"{_urls.BaseUrlFor(mix)}/my_page/play_data.php?lv={HttpUtility.UrlEncode(bucket)}",
            cancellationToken, client);
        var document = new HtmlDocument();
        document.LoadHtml(response);

        var grades = new List<(string Type, int Count)>();
        var plates = new List<(string Type, int Count)>();
        int? catalogTotal = null;
        var tiles = document.DocumentNode
            .SelectNodes("//a[contains(@class,'play_log_btn')][.//i[contains(@class,'t_num')]]");
        foreach (var tile in tiles ?? new HtmlNodeCollection(null))
        {
            var text = HttpUtility.HtmlDecode(
                tile.SelectSingleNode(".//i[contains(@class,'t_num')]")?.InnerText ?? "");
            var numbers = CountRegex.Matches(text)
                .Select(m => int.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture)).ToArray();
            if (numbers.Length == 0) continue;
            if (numbers.Length > 1) catalogTotal ??= numbers[1];

            var type = tile.GetAttributeValue("data-type", "");
            if (type.Length == 0) continue;
            // Phoenix omits data-division entirely and renders plate tiles only.
            if (tile.GetAttributeValue("data-division", "plate") == "grade") grades.Add((type, numbers[0]));
            else plates.Add((type, numbers[0]));
        }

        // Cumulative counts run best-to-worst, so the worst band present carries the total. Phoenix
        // states its own total instead, in the clear_w headline ("2,776/3,646").
        var cumulative = mix == MixEnum.Phoenix2;
        var passes = cumulative
            ? grades.Concat(plates).Select(t => t.Count).DefaultIfEmpty(0).Max()
            : 0;

        var clearText = document.DocumentNode
            .SelectSingleNode("//div[contains(@class,'clear_w')]//div[contains(@class,'t1')]")?.InnerText;
        if (clearText != null)
        {
            var parts = CountRegex.Matches(HttpUtility.HtmlDecode(clearText))
                .Select(m => int.Parse(m.Value.Replace(",", ""), CultureInfo.InvariantCulture)).ToArray();
            if (parts.Length > 0) passes = parts[0];
            if (parts.Length > 1) catalogTotal = parts[1];
        }

        return new PiuGameGetPlayDataResult
        {
            Bucket = bucket,
            Passes = passes,
            CatalogTotal = catalogTotal,
            GradeCounts = cumulative ? DeCumulate(grades) : Exact(grades),
            PlateCounts = cumulative ? DeCumulate(plates) : Exact(plates),
            Buckets = LevelBuckets(document)
        };
    }

    /// <summary>
    ///     Turns a best-to-worst cumulative run into per-band counts. A band the player has none of
    ///     is left out of the page entirely, and because the run is monotonic that can only ever be
    ///     a leading prefix — so walking what IS present, in document order, is exact.
    /// </summary>
    private static IReadOnlyDictionary<string, int> DeCumulate(IReadOnlyList<(string Type, int Count)> tiles)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var previous = 0;
        foreach (var (type, count) in tiles)
        {
            result[type] = count - previous;
            previous = count;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, int> Exact(IReadOnlyList<(string Type, int Count)> tiles)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (type, count) in tiles) result[type] = count;
        return result;
    }

    /// <summary>
    ///     The <c>?lv=</c> options, read off the page — the two mixes do not offer the same set
    ///     (Phoenix starts at 10, Phoenix 2 at 1) and assuming either one would silently miss levels.
    /// </summary>
    private static string[] LevelBuckets(HtmlDocument document)
    {
        var select = document.DocumentNode.SelectNodes("//select")
            ?.FirstOrDefault(s => s.GetAttributeValue("onchange", "")
                .Contains("lv=", StringComparison.OrdinalIgnoreCase));
        return select?.SelectNodes(".//option")
            ?.Select(o => o.GetAttributeValue("value", ""))
            .ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    ///     The official PUMBILITY pool. Both mixes serve it live at the same path in different
    ///     grammars, so the parser branches on the page shape rather than the mix — the same rule
    ///     the best-score walk follows.
    /// </summary>
    public async Task<PiuGameGetPumbilityResult> GetPumbility(MixEnum mix, HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries($"{_urls.BaseUrlFor(mix)}/my_page/pumbility.php",
            cancellationToken, client);
        var document = new HtmlDocument();
        document.LoadHtml(response);

        var totalText = HttpUtility.HtmlDecode(document.DocumentNode
            .SelectSingleNode("//div[contains(@class,'pumbility_total_wrap')]")?.InnerText ?? "");
        var totalMatch = DecimalRegex.Match(totalText);
        var total = totalMatch.Success
            ? double.Parse(totalMatch.Value.Replace(",", ""), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture)
            : 0;

        var cards = document.DocumentNode.SelectNodes("//li[div[contains(@class,'top-wrap')]]");
        var entries = cards is { Count: > 0 }
            ? ParsePumbilityCards(cards)
            : ParsePumbilityRows(document.DocumentNode.SelectNodes(
                "//div[contains(@class,'pumblitiySt')]//ul[contains(@class,'list')]/li"));

        return new PiuGameGetPumbilityResult { Total = total, Entries = entries };
    }

    /// <summary>Phoenix 2's breakdown cards: value in "354&lt;span&gt;.24&lt;/span&gt;", plate art
    /// prefixed "s_" unlike everywhere else on the site.</summary>
    private PiuGameGetPumbilityResult.Entry[] ParsePumbilityCards(HtmlNodeCollection cards)
    {
        var entries = new List<PiuGameGetPumbilityResult.Entry>();
        foreach (var card in cards)
            try
            {
                var inner = card.InnerHtml;
                var type = ShortChartType(inner);
                if (type == null) continue;

                var value = PumbilityValueRegex.Match(inner);
                if (!value.Success) continue;

                entries.Add(new PiuGameGetPumbilityResult.Entry
                {
                    SongName = SongFromLabel(HttpUtility.HtmlDecode(
                        card.SelectSingleNode(".//div[contains(@class,'mid-wrap')]")?.InnerText.Trim() ?? "")),
                    ChartType = type.Value,
                    Level = ShortLevel(inner),
                    Grade = GradeFrom(inner),
                    Plate = PlateFrom(inner),
                    Value = double.Parse(
                        value.Groups[1].Value.Replace(",", "") + "." + value.Groups[2].Value,
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing Phoenix 2 PUMBILITY card");
            }

        return entries.ToArray();
    }

    /// <summary>Phoenix's classic ranking rows: song in .profile_name .t1, contribution in
    /// .score i.tt, and no plate — Phoenix PUMBILITY never prices one.</summary>
    private PiuGameGetPumbilityResult.Entry[] ParsePumbilityRows(HtmlNodeCollection? rows)
    {
        var entries = new List<PiuGameGetPumbilityResult.Entry>();
        foreach (var row in rows ?? new HtmlNodeCollection(null))
            try
            {
                var inner = row.InnerHtml;
                var type = ShortChartType(inner);
                if (type == null) continue;

                var valueText = row
                    .SelectSingleNode(".//div[contains(@class,'score')]//i[contains(@class,'tt')]")?.InnerText;
                if (valueText == null) continue;

                entries.Add(new PiuGameGetPumbilityResult.Entry
                {
                    SongName = HttpUtility.HtmlDecode(row
                        .SelectSingleNode(".//div[contains(@class,'profile_name')]//p[contains(@class,'t1')]")
                        ?.InnerText.Trim() ?? ""),
                    ChartType = type.Value,
                    Level = ShortLevel(inner),
                    Grade = GradeFrom(inner),
                    Value = double.Parse(valueText.Replace(",", "").Trim(),
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing Phoenix PUMBILITY row");
            }

        return entries.ToArray();
    }

    /// <summary>
    ///     The charts behind one count tile. The site reaches them in two hops — a POST that
    ///     answers with a stub scripting the real GET — and this goes straight to that GET, which
    ///     is a read either way. Grade cells live on <c>detail2</c>, plate cells on <c>detail</c>.
    /// </summary>
    public async Task<PiuGameGetPlayLogResult> GetPlayLog(MixEnum mix, HttpClient client, string bucket,
        string type, bool isGrade, int page, CancellationToken cancellationToken)
    {
        var endpoint = isGrade ? "user_play_log_detail2.php" : "user_play_log_detail.php";
        var response = await GetWithRetries(
            $"{_urls.BaseUrlFor(mix)}/ajax/{endpoint}?lv={HttpUtility.UrlEncode(bucket)}" +
            $"&type={HttpUtility.UrlEncode(type)}&page={page}", cancellationToken, client);
        var document = new HtmlDocument();
        document.LoadHtml(response);

        var entries = new List<PiuGameGetPlayLogResult.Entry>();
        foreach (var li in document.DocumentNode.SelectNodes("//li[.//div[contains(@class,'song_name')]]")
                           ?? new HtmlNodeCollection(null))
        {
            var chartType = ShortChartType(li.InnerHtml);
            if (chartType == null) continue;

            entries.Add(new PiuGameGetPlayLogResult.Entry
            {
                SongName = HttpUtility.HtmlDecode(
                    li.SelectSingleNode(".//div[contains(@class,'song_name')]//p")?.InnerText.Trim()
                    ?? li.SelectSingleNode(".//div[contains(@class,'song_name')]")?.InnerText.Trim() ?? ""),
                ChartType = chartType.Value,
                Level = ShortLevel(li.InnerHtml)
            });
        }

        // The pager rewrites each button's onclick into "?lv=&type=X&&page=N"; the highest N it
        // offers is the last page (the window always includes it via the last-page button).
        var maxPage = PageNumberRegex.Matches(response)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(1).Max();

        return new PiuGameGetPlayLogResult { Entries = entries.ToArray(), MaxPage = maxPage };
    }

    /// <summary>
    ///     Phoenix 2's breakdown card prints title and artist as one text node joined by " - ",
    ///     with no element between them, so the title has to be recovered by splitting. Titles
    ///     contain the separator themselves — "Exceed2 Opening - SHORT CUT -" renders as
    ///     "Exceed2 Opening - SHORT CUT - - BanYa" — so the split is on the LAST occurrence, which
    ///     is right for every row on the page. An artist containing " - " would mis-split; the
    ///     charts are matched on type and level as well, so a bad title costs a match, not a wrong one.
    /// </summary>
    private static string SongFromLabel(string label)
    {
        var separator = label.LastIndexOf(" - ", StringComparison.Ordinal);
        return separator <= 0 ? label : label[..separator].Trim();
    }

    private static ChartType? ShortChartType(string html)
    {
        var match = ShortTypeRegex.Match(html);
        if (!match.Success) return null;
        return match.Groups[1].Value.ToLowerInvariant() switch
        {
            "s" => ChartType.Single,
            "d" => ChartType.Double,
            "c" => ChartType.CoOp,
            "sp" => ChartType.SinglePerformance,
            "dp" => ChartType.DoublePerformance,
            _ => null
        };
    }

    // A missing level reads as 29, matching the best-score parser: the site drops the digits on
    // its unnumbered top-tier stepballs.
    private static int ShortLevel(string html)
    {
        var digits = string.Join("", ShortLevelRegex.Matches(html).Select(m => m.Groups[1].Value));
        return digits.Length == 0 ? 29 : int.Parse(digits, CultureInfo.InvariantCulture);
    }

    private static PhoenixLetterGrade? GradeFrom(string html)
    {
        var match = GradeImageRegex.Match(html);
        if (!match.Success) return null;
        // A failed stage renders the same grade art under an "x_" prefix (x_aa_p.png). The
        // grade underneath is real, and a failed stage is where low scores live — the one
        // place the site ever prints a grade for a sub-800k score. Without the strip those
        // stems fall through to null and every broken play's grade is silently discarded.
        var stem = match.Groups[1].Value.ToLowerInvariant();
        if (stem.StartsWith("x_", StringComparison.Ordinal)) stem = stem[2..];
        return stem switch
        {
            "f" => PhoenixLetterGrade.F,
            "d" => PhoenixLetterGrade.D,
            "c" => PhoenixLetterGrade.C,
            "b" => PhoenixLetterGrade.B,
            "a" => PhoenixLetterGrade.A,
            "a_p" => PhoenixLetterGrade.APlus,
            "aa" => PhoenixLetterGrade.AA,
            "aa_p" => PhoenixLetterGrade.AAPlus,
            "aaa" => PhoenixLetterGrade.AAA,
            "aaa_p" => PhoenixLetterGrade.AAAPlus,
            "s" => PhoenixLetterGrade.S,
            "s_p" => PhoenixLetterGrade.SPlus,
            "ss" => PhoenixLetterGrade.SS,
            "ss_p" => PhoenixLetterGrade.SSPlus,
            "sss" => PhoenixLetterGrade.SSS,
            "sss_p" => PhoenixLetterGrade.SSSPlus,
            _ => null
        };
    }

    // The breakdown page prefixes its plate art "s_" (s_mg.png) where the rest of the site
    // serves it bare, so this accepts either.
    private static PhoenixPlate? PlateFrom(string html)
    {
        var match = PlateImageRegex.Match(html);
        if (!match.Success) return null;
        try
        {
            return PhoenixPlateHelperMethods.ParseShorthand(match.Groups[1].Value);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    // Avatar hosting differs BY LOGIN ERA: Phoenix pages serve /data/avatar_img/, Phoenix 2
    // pages serve /data/avatar_img2/ (verified 2026-07-09). Accept both — narrowing this to
    // one variant is the recurring avatar-import bug; both shapes are pinned by approval
    // fixtures (GetSongLeaderboard_HappyPath vs _Phoenix2Host, GetAccountData_Phoenix2Avatar).
    private static readonly Regex ImageRegex =
        new(
            @"url\(\'(https\:\/\/(?:phoenix\.)?piugame\.com\/data\/(avatar|song)_img2?\/[A-Za-z0-9]+\.[A-Za-z]+\?v\=[0-9]+)\'\)",
            RegexOptions.Compiled);

    public async Task<PiuGameGetAccountDataResult> GetAccountData(MixEnum mix, HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries($"{_urls.BaseUrlFor(mix)}/my_page/title.php",
            cancellationToken, client);


        var document = new HtmlDocument();
        document.LoadHtml(response);
        var lis = document.DocumentNode.SelectNodes(".//ul[contains(@class,'data_titleList2')]/li");
        if (lis == null)
            return new PiuGameGetAccountDataResult
            {
                AccountName = "INVALID",
                ImageUrl = new Uri("/notset", UriKind.Relative),
                // The wrong-password shape is the site's login page (login_wrap); an
                // authenticated account with no game profile associated (everyone's
                // launch-week state on Phoenix 2) renders my_page without the title list.
                RequiresLogin =
                    document.DocumentNode.SelectSingleNode(".//div[contains(@class,'login_wrap')]") != null
            };

        var titles = (from li in document.DocumentNode.SelectNodes(".//ul[contains(@class,'data_titleList2')]/li")
            let has = li.GetAttributeValue("class", "") == "have"
            let col = li.SelectSingleNode(".//p").GetAttributeValue("class", "")
                .Split(" ")
                .FirstOrDefault(c => c.StartsWith("col")) ?? ""
            let name = li.GetAttributeValue("data-name", "")
            select new PiuGameGetAccountDataResult.TitleEntry { ColClass = col, Have = has, Name = name }).ToArray();

        var accountName = document.DocumentNode
            .SelectSingleNode(".//div[contains(@class,'name_w')]/p[contains(@class,'t2')]")?.InnerText ?? "INVALID";
        var imageString = document
                              .DocumentNode.SelectSingleNode(".//div[contains(@class,'profile_img')]/div/div")
                              .GetAttributeValue("style", "")
                          ?? "";

        var imagePath = ImageRegex.Match(imageString).Groups[1].Value;
        return new PiuGameGetAccountDataResult
        {
            AccountName = accountName,
            ImageUrl = new Uri(imagePath, UriKind.Absolute),
            TitleEntries = titles
        };
    }

    public async Task<PiuGameGetUcsResult?> GetUcs(int ucsId, CancellationToken cancellationToken)
    {
        var response = await GetWithRetries($"{_urls.UcsBaseUrl}/bbs/board.php?bo_table=ucs_share&wr_id={ucsId}",
            cancellationToken);

        var document = new HtmlDocument();
        document.LoadHtml(response);
        var chartBox = document.DocumentNode.SelectSingleNode(".//div[contains(@class,'box1')]");
        if (chartBox == null) return null;

        var stepBall = chartBox.SelectSingleNode(".//div[contains(@class,'stepBall_in')]");
        if (stepBall == null) return null;

        var songName = chartBox
            .SelectSingleNode(
                ".//div[contains(@class,'con_in')]/div[contains(@class,'ti_wrap')]/p[contains(@class,'t1')]")
            ?.InnerText.Trim();
        if (songName == null) return null;


        var typeMatch = ShortTypeRegex.Match(stepBall.GetAttributeValue("style", ""));
        var chartType = typeMatch.Success
            ? typeMatch.Groups[1].Value.ToLower() switch
            {
                "c" => ChartType.CoOp,
                "s" => ChartType.Single,
                "d" => ChartType.Double,
                "sp" => ChartType.SinglePerformance,
                "dp" => ChartType.DoublePerformance,
                _ => ChartType.SinglePerformance
            }
            : ChartType.SinglePerformance;

        var difficultyLevelUrls = chartBox
            .SelectNodes(@".//div[contains(@class,'numw')]//img")
            .Select(i => i.GetAttributeValue("src", "Unknown")).ToArray();
        var level = 0;
        foreach (var url in difficultyLevelUrls)
        {
            level *= 10;
            var match = ShortLevelRegex.Match(url);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var parsedLevel)) continue;

            level += parsedLevel;
        }

        var uploader = chartBox.SelectSingleNode(".//div[contains(@class,'stepPeople_in')]//i[contains(@class,'tt')]")
            ?.InnerText.Replace(" ", "").Trim();
        if (uploader == null) return null;

        var description = chartBox.SelectSingleNode(".//div[contains(@class,'page_con')]")?.InnerText ?? "";
        return new PiuGameGetUcsResult
        {
            ChartType = chartType,
            Description = description,
            Level = level,
            SongName = songName,
            Uploader = uploader
        };
    }

    public async Task<IEnumerable<GameCardRecord>> GetCards(MixEnum mix, HttpClient client,
        CancellationToken cancellationToken)
    {
        var html = await client.GetStringAsync($"{_urls.BaseUrlFor(mix)}/my_page/game_id_information.php",
            cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var profileBoxes = document.DocumentNode.SelectNodes(
            ".//div[contains(@id,'profile_modal')]//div[contains(@class,'in_profile')]");
        if (profileBoxes == null) return Array.Empty<GameCardRecord>();

        if (profileBoxes.Count == 0) return Array.Empty<GameCardRecord>();

        var mainId = document.DocumentNode
            .SelectSingleNode(
                ".//div[contains(@class,'subProfile_wrap')]//div[contains(@class,'name_w')]/p[contains(@class,'t2')]")
            ?.InnerText ?? "";
        if (string.IsNullOrWhiteSpace(mainId)) return Array.Empty<GameCardRecord>();

        return (from card in profileBoxes
            let tag = card.SelectSingleNode(".//div[contains(@class,'name_w')]/p[contains(@class,'t2')]")
                ?.InnerText ?? ""
            let id = card.SelectSingleNode(".//input[contains(@name,'sub_profile')]")
                ?.GetAttributeValue("value", "") ?? ""
            where !string.IsNullOrWhiteSpace(tag) && !string.IsNullOrWhiteSpace(id)
            select new GameCardRecord(tag, id, tag == mainId)).ToList();
    }

    public async Task SetCard(MixEnum mix, HttpClient client, string id, CancellationToken cancellationToken)
    {
        var result = await PostForMessageWithRetries($"{_urls.BaseUrlFor(mix)}/ajax/sub_profile.php",
            new Dictionary<string, string>
            {
                { "no", id }
            }, cancellationToken, client);
        result.EnsureSuccessStatusCode();
    }
}