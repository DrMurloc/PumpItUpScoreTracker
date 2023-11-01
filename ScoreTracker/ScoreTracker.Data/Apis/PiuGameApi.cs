using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using ScoreTracker.Data.Apis.Contracts;
using ScoreTracker.Data.Apis.Dtos;
using ScoreTracker.Domain.Enums;
using SendGrid.Helpers.Mail;

namespace ScoreTracker.Data.Apis
{
    public sealed class PiuGameApi : IPiuGameApi
    {
        private HttpClient _client;

        public PiuGameApi(HttpClient client)
        {
            _client = client;
        }

        private static readonly Regex LevelRegex =
            new(@"^https\:\/\/piugame\.com\/l_img\/stepball\/full\/[a-zA-Z]_num_([0-9])\.png$", RegexOptions.Compiled);

        private static readonly Regex TypeRegex =
            new(@"^https\:\/\/piugame\.com\/l_img\/stepball\/full\/([a-zA-Z])_text\.png$", RegexOptions.Compiled);

        private static readonly Regex
            IdRegex = new(@"over_ranking_view\.php\?no=([a-zA-Z0-9]+)", RegexOptions.Compiled);

        public async Task<PiuGameGetSongsResult> Get20AboveSongs(int page, CancellationToken cancellationToken)
        {
            var response = await _client.GetStringAsync(
                $"https://piugame.com/leaderboard/over_ranking.php?lv=&search=&&page={page}", cancellationToken);
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
                var typeMatch = TypeRegex.Match(chartTypeUrl);
                var chartType = typeMatch.Success
                    ? typeMatch.Groups[1].Value.ToLower() switch
                    {
                        "c" => ChartType.CoOp.ToString(),
                        "s" => ChartType.Single.ToString(),
                        "d" => ChartType.Double.ToString()
                    }
                    : ChartType.SinglePerformance.ToString();
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
                    Type = chartType
                });
            }

            var nextIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'next')]");
            var lastIcon = document.DocumentNode.SelectNodes("//i[contains(@class,'last')]");
            return new PiuGameGetSongsResult()
            {
                Results = result.ToArray(),
                IsEnd = (nextIcon == null || !nextIcon.Any()) && (lastIcon == null || !lastIcon.Any())
            };
        }


        public async Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(string songId,
            CancellationToken cancellationToken)
        {
            var response =
                await _client.GetStringAsync($"https://piugame.com/leaderboard/over_ranking_view.php?no={songId}",
                    cancellationToken);
            var document = new HtmlDocument();
            document.LoadHtml(response);
            var results = new List<PiuGameGetSongLeaderboardResult.EntryResultDto>();
            foreach (var li in document.DocumentNode.SelectNodes("//div[contains(@class,'rangking_list_w')]//li"))
            {
                var scoreNode = li.SelectSingleNode(
                    ".//div[contains(@class,'score')]//i[contains(@class,'tt')]");
                results.Add(new PiuGameGetSongLeaderboardResult.EntryResultDto
                {
                    Score = int.Parse(scoreNode.InnerText, NumberStyles.AllowThousands)
                });
            }

            return new PiuGameGetSongLeaderboardResult()
            {
                Results = results.ToArray()
            };
        }
    }
}
