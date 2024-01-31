using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.VisualBasic.FileIO;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Domain.Enums;

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

    public PiuGameApi(HttpClient client)
    {
        _client = client;
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

                results.Add(new PiuGameGetSongLeaderboardResult.EntryResultDto
                {
                    Score = int.Parse(scoreNode.InnerText, NumberStyles.AllowThousands),
                    ProfileName = profileName
                });
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
        var today = DateTimeOffset.Now;
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
            results.Add(new PiuGameGetChartPopularityLeaderboardResult.Entry
            {
                ChartLevel = level,
                ChartType = chartType,
                Place = place,
                SongName = HttpUtility.HtmlDecode(scoreP.InnerText)
            });
        }

        return new PiuGameGetChartPopularityLeaderboardResult
        {
            Entries = results.ToArray()
        };
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

    private async Task<string> GetWithRetries(string url, CancellationToken cancellationToken = default)
    {
        var retry = 0;
        while (true)
            try
            {
                return await _client.GetStringAsync(url, cancellationToken);
                break;
            }
            catch
            {
                if (retry == 3) throw;

                retry++;
                await Task.Delay(1000, cancellationToken);
            }
    }

    private ChartType GetChartTypeFromUrl(string chartTypeUrl)
    {
        var typeMatch = TypeRegex.Match(chartTypeUrl);
        return typeMatch.Success
            ? typeMatch.Groups[1].Value.ToLower() switch
            {
                "c" => ChartType.CoOp,
                "s" => ChartType.Single,
                "d" => ChartType.Double
            }
            : ChartType.SinglePerformance;
    }

    /*
     *
     *      <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <div class="img_wrap">
          <span class="medal_wrap"><i class="img"><img src="/l_img/silvermedal.png" /></i></span>
        </div>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">2</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/ed7e43efd28eba896f90b94ff1ebc06f.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">After LIKE</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <div class="img_wrap">
          <span class="medal_wrap"><i class="img"><img src="/l_img/bronzemedal.png" /></i></span>
        </div>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">154</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/e0cf19dbb807e5d3f2efa3db5ca163a0.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">ELEVEN</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">4</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">21</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/14cd5d7a3df1f12b82bccec2faea2705.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Neo Catharsis</p>
      <p class="t2">TAG underground overlay</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">5</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">2</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/7b6237f4583cab1dd1a1b1f85264eaa5.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Nxde</p>
      <p class="t2">(G)I-DLE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">6</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">95</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/e0cf19dbb807e5d3f2efa3db5ca163a0.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">ELEVEN</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">7</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">37</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/e0cf19dbb807e5d3f2efa3db5ca163a0.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">ELEVEN</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_5.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">8</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">27</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/14cd5d7a3df1f12b82bccec2faea2705.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Neo Catharsis</p>
      <p class="t2">TAG underground overlay</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">9</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">12</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/e0cf19dbb807e5d3f2efa3db5ca163a0.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">ELEVEN</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_7.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">10</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">1</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/7e1af52be6d8b4e147d2a0ebbf54ef98.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Euphorianic</p>
      <p class="t2">SHK</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">11</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">16</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/dde154d8a721c5d510b71c788d0b8673.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Viyella's Nightmare</p>
      <p class="t2">Laur</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">12</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">4</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/3f60d49fc1d14e5cfa51e8c90eec847b.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Pneumonoultramicroscopicsilicovolcanoconiosis ft. Kagamine Len/GUMI</p>
      <p class="t2">DASU</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">13</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">20</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/14cd5d7a3df1f12b82bccec2faea2705.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Neo Catharsis</p>
      <p class="t2">TAG underground overlay</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_7.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">14</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">8</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/dde154d8a721c5d510b71c788d0b8673.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Viyella's Nightmare</p>
      <p class="t2">Laur</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">15</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">16</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/d24baf611d258d15c997afc26b6380fe.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Barber's Madness</p>
      <p class="t2">Klass E</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">16</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">10</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/e0cf19dbb807e5d3f2efa3db5ca163a0.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">ELEVEN</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">17</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">1</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/ed7e43efd28eba896f90b94ff1ebc06f.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">After LIKE</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_3.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">18</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">18</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/d24baf611d258d15c997afc26b6380fe.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Barber's Madness</p>
      <p class="t2">Klass E</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">19</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">4</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/3f60d49fc1d14e5cfa51e8c90eec847b.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Pneumonoultramicroscopicsilicovolcanoconiosis ft. Kagamine Len/GUMI</p>
      <p class="t2">DASU</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">20</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">2</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/ed7e43efd28eba896f90b94ff1ebc06f.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">After LIKE</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_5.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">21</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">1</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/7b6237f4583cab1dd1a1b1f85264eaa5.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Nxde</p>
      <p class="t2">(G)I-DLE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">22</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">2</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/5c0a9e5e699863547ef7fa8d6485fc4a.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Teddy Bear</p>
      <p class="t2">STAYC</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">23</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">19</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/d24baf611d258d15c997afc26b6380fe.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Barber's Madness</p>
      <p class="t2">Klass E</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">24</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">32</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/268d0e5526dc475ccf8ca90a60fcae68.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Aragami</p>
      <p class="t2">xi</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">25</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">5</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/584749a3cecd3f4b5714df28cd502d14.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Etude Op 10-4</p>
      <p class="t2">MAX</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">26</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">8</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/ed7e43efd28eba896f90b94ff1ebc06f.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">After LIKE</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">27</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">31</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/dde154d8a721c5d510b71c788d0b8673.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Viyella's Nightmare</p>
      <p class="t2">Laur</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_3.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">28</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">134</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/dde154d8a721c5d510b71c788d0b8673.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Viyella's Nightmare</p>
      <p class="t2">Laur</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">29</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">58</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/e0cf19dbb807e5d3f2efa3db5ca163a0.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">ELEVEN</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">30</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">21</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/14cd5d7a3df1f12b82bccec2faea2705.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Neo Catharsis</p>
      <p class="t2">TAG underground overlay</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_1.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">31</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">16</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/ed7e43efd28eba896f90b94ff1ebc06f.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">After LIKE</p>
      <p class="t2">IVE</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_4.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">32</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">9</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/7e1af52be6d8b4e147d2a0ebbf54ef98.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Euphorianic</p>
      <p class="t2">SHK</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">33</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">1</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/584749a3cecd3f4b5714df28cd502d14.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Etude Op 10-4</p>
      <p class="t2">MAX</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">34</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">3</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/7e1af52be6d8b4e147d2a0ebbf54ef98.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Euphorianic</p>
      <p class="t2">SHK</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">35</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">36</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/14cd5d7a3df1f12b82bccec2faea2705.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Neo Catharsis</p>
      <p class="t2">TAG underground overlay</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_5.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">36</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">19</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/eb5af940defb17449259e75e16926870.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">BOCA</p>
      <p class="t2">Dreamcatcher</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">37</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">35</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/6175376a00ff0d561bab24934e04b782.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Halloween Party ~Multiverse~</p>
      <p class="t2">SHK</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">38</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">35</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/6175376a00ff0d561bab24934e04b782.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Halloween Party ~Multiverse~</p>
      <p class="t2">SHK</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">39</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">4</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/5c0a9e5e699863547ef7fa8d6485fc4a.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Teddy Bear</p>
      <p class="t2">STAYC</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">40</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">10</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/5c0a9e5e699863547ef7fa8d6485fc4a.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Teddy Bear</p>
      <p class="t2">STAYC</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_4.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">41</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">21</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/268d0e5526dc475ccf8ca90a60fcae68.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Aragami</p>
      <p class="t2">xi</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">42</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">21</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/7e1af52be6d8b4e147d2a0ebbf54ef98.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Euphorianic</p>
      <p class="t2">SHK</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">43</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">5</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/8eb8a3021756670ce067a3ef7cfb9f2a.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">MURDOCH</p>
      <p class="t2">WONDERTRAVELER Project</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">44</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">30</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/268d0e5526dc475ccf8ca90a60fcae68.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Aragami</p>
      <p class="t2">xi</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_7.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">45</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">45</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/950bba94b66e30b4a33c794c204b2ac8.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">GOOD NIGHT</p>
      <p class="t2">Dreamcatcher</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">46</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">19</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/d24baf611d258d15c997afc26b6380fe.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Barber's Madness</p>
      <p class="t2">Klass E</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_9.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">47</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-down-min"></i><i class="num">1</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/ab914c8ca7030b776ec3cd3ad7dad3e9.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">VECTOR</p>
      <p class="t2">Zekk</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_8.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">48</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">21</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/a7b3d3aadddc0fce1160f509054f1686.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Beethoven Virus</p>
      <p class="t2">BanYa</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_0.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_7.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">49</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">77</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/d24baf611d258d15c997afc26b6380fe.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Barber's Madness</p>
      <p class="t2">Klass E</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">50</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">11</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/3f60d49fc1d14e5cfa51e8c90eec847b.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Pneumonoultramicroscopicsilicovolcanoconiosis ft. Kagamine Len/GUMI</p>
      <p class="t2">DASU</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/d_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/d_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_2.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/d_num_2.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
  <li>
<div class="in flex vc wrap">
  <div class="in_layOut flex vc wrap">
    <div class="num">
                  <i class="tt">51</i>
              </div>
    <div class="num_ranking"><div class="tt flex vc"><i class="xi xi-caret-up-min"></i><i class="num">2</i></div></div>
  </div>
  <div class="name flex vc wrap">
    <div class="profile_img"><div class="resize"><div class="re bgfix" style="background-image:url('https://piugame.com/data/song_img/305329152cdd89f6229dec2a3074c18b.png?v=20231121134107')"></div></div></div>
    <div class="profile_name">
      <p class="t1">Versailles</p>
      <p class="t2">HyuN &amp; MIIM</p>
    </div>
  </div>
  <div class="level_wrap">
    <div class="stepBall_img_wrap">
      <div class="stepBall_in flex vc col hc wrap bgfix cont" style="background-image:url('https://piugame.com/l_img/stepball/full/s_bg.png')">
        <div class="tw"><img src="https://piugame.com/l_img/stepball/full/s_text.png" alt=""></div>
        <div class="numw flex vc hc">
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_1.png" alt=""></div>
          <div class="imG"><img src="https://piugame.com/l_img/stepball/full/s_num_6.png" alt=""></div>
        </div>
      </div>
    </div>
  </div>
</div>
  </li>
<script>
  $('.more-btn').show();
$('.loading-btn').hide();
</script>



     */
}