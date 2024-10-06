using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Apis;

public sealed class PiuGameApi : IPiuGameApi
{
    private static readonly Regex LevelRegex =
        new(@"^https\:\/\/piugame\.com\/l_img\/stepball\/full\/[a-zA-Z]_num_([0-9])\.png$", RegexOptions.Compiled);

    private static readonly Regex TypeRegex =
        new(@"^https\:\/\/piugame\.com\/l_img\/stepball\/full\/([a-zA-Z])_text\.png$", RegexOptions.Compiled);

    private static readonly Regex
        IdRegex = new(@"over_ranking_view\.php\?no=([a-zA-Z0-9]+)", RegexOptions.Compiled);

    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly ICurrentUserAccessor _currentUser;

    public PiuGameApi(HttpClient client, ILogger<PiuGameApi> logger, ICurrentUserAccessor currentUser)
    {
        _client = client;
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<PiuGameGetSongsResult> Get20AboveSongs(int page, CancellationToken cancellationToken)
    {
        var response = await GetWithRetries(
            $"https://piugame.com/leaderboard/over_ranking.php?lv=&search=&&page={page}",
            cancellationToken);
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

            songName = HttpUtility.HtmlDecode(songName);
            if (songName.Contains("End of a Dream"))
                songName = "Re:End of a Dream";
            else if (songName.Contains("CROSS RAY"))
                songName = "Cross Ray";
            else if (songName.Contains("Kasou Shinja") &&
                     !songName.Contains("SHORT", StringComparison.OrdinalIgnoreCase))
                songName = "Kasou Shinja";
            else if (songName.Contains("Yoropiku Pikuyoro")) songName = "Yoropiku Pikuyoro!";
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


    public async Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(string songId,
        CancellationToken cancellationToken)
    {
        var response =
            await GetWithRetries($"https://piugame.com/leaderboard/over_ranking_view.php?no={songId}",
                cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(response);
        var results = new List<PiuGameGetSongLeaderboardResult.EntryResultDto>();
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
                        Score = int.Parse(scoreNode.InnerText, NumberStyles.AllowThousands),
                        ProfileName = profileName,
                        AvatarUrl = new Uri(ImageRegex.Match(avatarNode.GetAttributeValue("style", "")).Groups[1].Value)
                    });
                }
                catch (Exception e)
                {
                    //
                }
            }

        return new PiuGameGetSongLeaderboardResult
        {
            Results = results.ToArray()
        };
    }

    public async Task<PiuGameGetLeaderboardListResult> GetLeaderboards(CancellationToken cancellationToken)
    {
        var response =
            await GetWithRetries("https://piugame.com/leaderboard/rating_ranking.php", cancellationToken);

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

    public async Task<PiuGameGetLeaderboardResult> GetLeaderboard(string leaderboardId,
        CancellationToken cancellationToken)
    {
        var response =
            await GetWithRetries("https://piugame.com/leaderboard/rating_ranking.php?lv=" + leaderboardId,
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
                NumberStyles.AllowThousands);
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

    public async Task<PiuGameGetChartPopularityLeaderboardResult> GetChartPopularityLeaderboard(int page,
        CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.Now - TimeSpan.FromDays(1);
        var response = await PostWithRetries("https://piugame.com/ajax/top_steps.php",
            new Dictionary<string, string>
            {
                { "page", page.ToString() },
                { "date", $"{today.Year}0{today.Month}" },
                { "mode", "full" }
            }, cancellationToken);
        var results = new List<PiuGameGetChartPopularityLeaderboardResult.Entry>();
        var document = new HtmlDocument();
        document.LoadHtml(response);
        var lis = document.DocumentNode.SelectNodes("./li");
        if (lis == null)
            return new PiuGameGetChartPopularityLeaderboardResult
            {
                Entries = results.ToArray()
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

            if (level == 0) continue;
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
            Entries = results.ToArray()
        };
    }

    public async Task<IEnumerable<PiuGameGetRecentScoresResult>> GetRecentScores(HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries("https://piugame.com/my_page/recently_played.php",
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
                                          ?.InnerText ??
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

                var perfects = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'PERFECT')]/div").InnerText,
                    NumberStyles.AllowThousands);
                var greats = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'GREAT')]/div").InnerText,
                    NumberStyles.AllowThousands);
                var goods = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'GOOD')]/div").InnerText,
                    NumberStyles.AllowThousands);
                var bads = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'BAD')]/div").InnerText,
                    NumberStyles.AllowThousands);
                var misses = int.Parse(card.SelectSingleNode(".//td[contains(@data-th,'MISS')]/div").InnerText,
                    NumberStyles.AllowThousands);
                var scoreScreen = new ScoreScreen(perfects, greats, goods, bads, misses, 0);
                var plate = scoreScreen.PlateText;
                results.Add(new PiuGameGetRecentScoresResult
                {
                    ChartType = chartType!.Value,
                    Level = level,
                    NoteCount = perfects + greats + goods + bads + misses,
                    Plate = plate,
                    SongName = songName,
                    IsBroken = isBroken,
                    Score = score
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

    private async Task<string> PostWithRetries(string url, IDictionary<string, string> form,
        CancellationToken cancellationToken = default)
    {
        var retry = 0;
        while (true)
            try
            {
                var response = await _client.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
                break;
            }
            catch
            {
                if (retry == 3) throw;

                retry++;
                await Task.Delay(1000, cancellationToken);
            }
    }

    private async Task<HttpResponseMessage> PostForMessageWithRetries(string url, IDictionary<string, string> form,
        CancellationToken cancellationToken = default, HttpClient? client = null)
    {
        var retry = 0;
        while (true)
            try
            {
                var response =
                    await (client ?? _client).PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);

                //response.EnsureSuccessStatusCode();
                return response;
                break;
            }
            catch
            {
                if (retry == 3) throw;

                retry++;
                await Task.Delay(1000, cancellationToken);
            }
    }

    private async Task<string> GetWithRetries(string url, CancellationToken cancellationToken = default,
        HttpClient? client = null)
    {
        var retry = 0;
        while (true)
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await (client ?? _client).SendAsync(request, cancellationToken);
                return await response.Content.ReadAsStringAsync(cancellationToken);
                break;
            }
            catch
            {
                if (retry == 3) throw;

                retry++;
                await Task.Delay(1000, cancellationToken);
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
                _ => null
            }
            : ChartType.SinglePerformance;
    }

    public async Task<HttpClient> GetSessionId(string username, string password, CancellationToken cancellationToken)
    {
        var webRequestHandler = new HttpClientHandler();
        var client = new HttpClient(webRequestHandler);
        client.DefaultRequestHeaders.Add("origin", "https://piugame.com");

        await client.GetAsync("https://piugame.com", cancellationToken);

        var response = await PostForMessageWithRetries("https://piugame.com/bbs/login_check.php",
            new Dictionary<string, string>
            {
                { "url", "/" },
                { "mb_id", username },
                { "mb_password", password }
            }, cancellationToken, client);
        //return "";
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        return client;
    }

    public async Task<PiuGameGetBestScoresResult> GetBestScores(HttpClient client, int page,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries($"https://piugame.com/my_page/my_best_score.php?&&page={page}",
            cancellationToken, client);

        var document = new HtmlDocument();
        document.LoadHtml(response);
        var lastI = document.DocumentNode.SelectNodes(".//i[contains(@class,'last')]")?.First();
        var maxPageStrings = lastI?.ParentNode
            .GetAttributeValue("onclick", "")
            .Split("=") ?? Array.Empty<string>();
        var maxPage =
            maxPageStrings.Length > 0 ? int.Parse(maxPageStrings[^1].TrimEnd('\'') ?? "") : page;

        var foundScores =
            document.DocumentNode.SelectNodes(
                ".//ul[contains(@class,'my_best_scoreList')]/li/div[contains(@class,'in')]");
        var result = new PiuGameGetBestScoresResult
        {
            MaxPage = maxPage
        };
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

            var difficulty = string.Join("",
                scoreCard.SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'imG')]//img")
                    .Select(n => n.GetAttributeValue("src", "")
                        .Substring(46, 1))).TrimStart('.');

            var scoreList = scoreCard.SelectNodes(".//div[contains(@class,'etc_con')]//ul").First();

            var score = scoreList.ChildNodes[1].ChildNodes[1].ChildNodes[1].ChildNodes[0].InnerText.Trim()
                .Replace(",", "");
            var plate = scoreList.ChildNodes[5].ChildNodes[1].ChildNodes[1].ChildNodes[0]
                .GetAttributeValue("src", "").Substring(32, 2);
            try
            {
                scores.Add(new PiuGameGetBestScoresResult.ScoreDto
                {
                    ChartType = chartType!.Value,
                    Level = int.Parse(difficulty),
                    Plate = PhoenixPlateHelperMethods.ParseShorthand(plate),
                    Score = int.Parse(score),
                    SongName = songName
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error parsing best score");
            }
        }

        result.Scores = scores.ToArray();
        return result;

        /*
    var csvString="data:text/csv;charset=utf-8,";
        csvString+="Song,Difficulty,Score,LetterGrade,Plate\r\n";
        var pageIndex=1;
        while(true) {

            var nextPageString = await $.get("https://piugame.com/my_page/my_best_score.php?&&page="+pageIndex)
            var page=$(nextPageString);
            var foundScores=$("ul.my_best_scoreList>li>div.in",page);
            foundScores.each(function(){
                var songName = $('.song_name',this)[0].children[0].innerText.replaceAll('"','""').replaceAll('#','Num');
                var chartType = $($('.stepBall_img_wrap .imG img',this)[0]).attr("src").substring(40,41);
                var difficultyLevel = $('.stepBall_img_wrap .imG img',this).map((index,i)=> $(i).attr("src").substring(46,47)).get().join("");

                var scoreList = $(".etc_con>ul",this)[0];
                var letter = $(scoreList.children[1].children[0].children[0].children[0]).attr('src').substring(32).replace(".png","").replace("_p","+");
                var score = $(scoreList.children[0].children[0].children[0].children[0])[0].innerText.replaceAll(",","");
                var plate = $(scoreList.children[2].children[0].children[0].children[0]).attr("src").substring(32,34);
                csvString+='"'+songName+'",'+chartType+difficultyLevel+","+score+","+letter+","+plate+"\r\n";
            });
            pageIndex++;
            console.log("Page "+pageIndex);
            if($(".xi.last",page).length==0){
                break;
            }
        }
        console.log(csvString);
        var encodedUri = encodeURI(csvString);
        window.open(encodedUri);
         */
    }

    private static readonly Regex ImageRegex =
        new(
            @"url\(\'(https\:\/\/piugame\.com\/data\/(avatar|song)_img\/[A-Za-z0-9]+\.[A-Za-z]+\?v\=[0-9]+)\'\)",
            RegexOptions.Compiled);

    public async Task<PiuGameGetAccountDataResult> GetAccountData(HttpClient client,
        CancellationToken cancellationToken)
    {
        var response = await GetWithRetries("https://piugame.com/my_page/title.php",
            cancellationToken, client);


        var document = new HtmlDocument();
        document.LoadHtml(response);
        var lis = document.DocumentNode.SelectNodes(".//ul[contains(@class,'data_titleList2')]/li");
        if (lis == null)
            return new PiuGameGetAccountDataResult
            {
                AccountName = "INVALID",
                ImageUrl = new Uri("/notset", UriKind.Relative)
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
}