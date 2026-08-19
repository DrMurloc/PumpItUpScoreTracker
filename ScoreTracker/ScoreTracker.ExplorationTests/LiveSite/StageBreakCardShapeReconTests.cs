using System.Text;
using System.Web;
using HtmlAgilityPack;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     Read-only instrument for the broken-vs-stage-broken question: what does each my_page card
///     surface render for (a) a stage that was interrupted (life ran out, song cut short) versus
///     (b) a stage that was played to the end but failed? The recently-played pages print
///     "STAGE BREAK" in place of the score for (a); the redesigned best list prints a number for
///     both. The one signal that could separate them without a judgement breakdown is the grade
///     image — a failed-but-finished stage renders an <c>x_</c>-prefixed grade, an interrupted
///     one has no grade to render — so this walks every best page and both recently-played pages
///     and reports, per card, the score text, the grade image stem, whether a plate is present,
///     and the judgement sum. Nothing here writes anywhere.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class StageBreakCardShapeReconTests : IClassFixture<PiuGameSessionFixture>
{
    private const int MaxBestPagesToWalk = 60;
    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public StageBreakCardShapeReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task Broken_cards_report_their_grade_image_and_score_shape_on_every_surface()
    {
        var ct = CancellationToken.None;
        var report = new StringBuilder();

        var p2 = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        var firstPage = await _fixture.Api.GetBestScores(MixEnum.Phoenix2, p2, 1, ct);
        var pages = Math.Min(firstPage.MaxPage, MaxBestPagesToWalk);
        report.AppendLine($"== Phoenix 2 best list: {firstPage.MaxPage} pages, walking {pages}");

        var brokenBest = new List<string>();
        var total = 0;
        for (var page = 1; page <= pages; page++)
        {
            var html = await p2.GetStringAsync($"https://piugame.com/my_page/my_best_score.php?&&page={page}", ct);
            foreach (var card in Cards(html))
            {
                total++;
                var shape = Shape(card);
                if (shape.HasPlate) continue;
                brokenBest.Add($"  p{page,-3} {shape}");
            }
        }

        report.AppendLine($"  {total} cards, {brokenBest.Count} without a plate:");
        foreach (var line in brokenBest) report.AppendLine(line);
        report.AppendLine(
            $"  grade-stem histogram of the plate-less cards: {Histogram(brokenBest.Select(l => l.Split(" grade=")[1].Split(' ')[0]))}");

        report.AppendLine("== Phoenix 2 recently played");
        var p2Recent = await p2.GetStringAsync("https://piugame.com/my_page/recently_played.php", ct);
        foreach (var card in Cards(p2Recent)) report.AppendLine($"  {Shape(card)}");

        var p1 = await _fixture.GetAuthenticatedClient(ct);
        report.AppendLine("== Phoenix recently played");
        var p1Recent = await p1.GetStringAsync("https://phoenix.piugame.com/my_page/recently_played.php", ct);
        foreach (var card in Cards(p1Recent)) report.AppendLine($"  {Shape(card)}");

        _output.WriteLine(report.ToString());
        Assert.True(total > 0, "No best cards parsed — the redesigned best list has changed shape.");
    }

    private static IEnumerable<HtmlNode> Cards(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document.DocumentNode.SelectNodes(".//ul[contains(@class,'recently_playeList')]/li")
               ?? new HtmlNodeCollection(null);
    }

    private static string Histogram(IEnumerable<string> stems)
    {
        return string.Join(", ", stems.GroupBy(s => s).OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}×{g.Count()}"));
    }

    private sealed record CardShape(string Song, string Ball, string ScoreText, string GradeStem, bool HasPlate,
        string Judgements, string Date)
    {
        public override string ToString()
        {
            return $"{Song,-32} {Ball,-6} score={ScoreText,-12} grade={GradeStem,-8} plate={(HasPlate ? "yes" : "no "),-3} " +
                   $"judg={Judgements,-24} {Date}";
        }
    }

    private static CardShape Shape(HtmlNode card)
    {
        var song = HttpUtility.HtmlDecode(card.SelectSingleNode(".//div[contains(@class,'song_name')]/p")?.InnerText ?? "?");
        var typeStem = Stem(card.SelectSingleNode(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'tw')]//img")
            ?.GetAttributeValue("src", ""));
        var digits = string.Join("", card
            .SelectNodes(".//div[contains(@class,'stepBall_img_wrap')]//div[contains(@class,'numw')]//img")
            ?.Select(i => Stem(i.GetAttributeValue("src", "")).Split('_').Last()) ?? Array.Empty<string>());
        var scoreCell = card.SelectSingleNode(".//div[contains(@class,'li_in') and contains(@class,'ac')]");
        var scoreText = scoreCell?.SelectSingleNode("./i[contains(@class,'tx')]")?.InnerText.Trim() ?? "?";
        var gradeSrc = scoreCell?.SelectSingleNode("./img")?.GetAttributeValue("src", "") ?? "";
        var gradeStem = gradeSrc.Length == 0 ? "EMPTY" : Stem(gradeSrc);
        var hasPlate = card.SelectNodes(".//div[contains(@class,'li_in')]/img")
            ?.Any(i => i.GetAttributeValue("src", "").Contains("/plate/")) ?? false;
        var judgements = string.Join("/", new[] { "PERFECT", "GREAT", "GOOD", "BAD", "MISS" }
            .Select(j => card.SelectSingleNode($".//td[contains(@data-th,'{j}')]/div")?.InnerText.Trim() ?? "-"));
        var date = card.SelectSingleNode(".//p[contains(@class,'recently_date_tt')]")?.InnerText.Trim() ?? "";
        var ball = typeStem.Length == 0 ? $"?{digits}" : $"{typeStem[..1].ToUpperInvariant()}{digits}";
        return new CardShape(song, ball, scoreText, gradeStem, hasPlate, judgements, date);
    }

    private static string Stem(string? src)
    {
        if (string.IsNullOrEmpty(src)) return "";
        var file = src.Split('/').Last().Split('?')[0];
        return file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
    }
}
