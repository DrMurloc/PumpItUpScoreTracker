using System.Text;
using ScoreTracker.SharedKernel.Enums;
using Xunit.Abstractions;

namespace ScoreTracker.ExplorationTests.LiveSite;

/// <summary>
///     What the Phoenix 2 best list's date actually means. Two readings are in play: it is the
///     date the displayed score was set, or it is a date that does NOT move when the score
///     improves (the chart's first play, or whenever the card was created). The import's journal
///     key is the card's date, so the answer decides whether a later pass lands on its own row or
///     on an earlier attempt's.
///     <para>
///         The measurement needs no database: the recently-played window carries each play's own
///         time, so a chart whose window holds a play at exactly the best card's score dates that
///         score independently. If the card's rule were "when you set this score", the two stamps
///         would agree on every such chart.
///     </para>
///     <para>Read-only, and it walks the best list once. Nothing is written anywhere.</para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class BestCardDateSemanticsReconTests : IClassFixture<PiuGameSessionFixture>
{
    private const int MaxBestPagesToWalk = 60;
    private readonly PiuGameSessionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public BestCardDateSemanticsReconTests(PiuGameSessionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [LiveSiteFact]
    public async Task Does_the_best_cards_date_move_when_the_score_does()
    {
        var ct = CancellationToken.None;
        var client = await _fixture.GetAuthenticatedPhoenix2Client(ct);
        var report = new StringBuilder();

        var firstPage = await _fixture.Api.GetBestScores(MixEnum.Phoenix2, client, 1, ct);
        var pages = Math.Min(firstPage.MaxPage, MaxBestPagesToWalk);
        var bests = new List<(string Song, ChartType Type, int Level, int Score, DateTimeOffset? At)>();
        for (var page = 1; page <= pages; page++)
        {
            var result = page == 1 ? firstPage : await _fixture.Api.GetBestScores(MixEnum.Phoenix2, client, page, ct);
            foreach (var s in result.Scores)
                bests.Add((s.SongName.ToString(), s.ChartType, (int)s.Level, (int)s.Score, s.RecordedAt));
        }

        var window = (await _fixture.Api.GetRecentScores(MixEnum.Phoenix2, client, ct)).ToArray();
        report.AppendLine($"== best list: {bests.Count} cards over {pages} page(s); window: {window.Length} plays");

        // The named case: a chart failed, then passed days later. The pass's own time is known
        // from this site's recently-played page; the question is what the best card says.
        foreach (var card in bests.Where(b => b.Song.StartsWith("Rush-More", StringComparison.OrdinalIgnoreCase)))
            report.AppendLine($"   Rush-More card: {card.Type}{card.Level} score={card.Score:N0} date={card.At:yyyy-MM-dd HH:mm:ss}");

        report.AppendLine();
        report.AppendLine("== charts whose window holds a play at exactly the best card's score");
        report.AppendLine("   (if the card dated the score, card date == that play's time on every row)");
        var agree = 0;
        var disagree = 0;
        foreach (var card in bests)
        {
            var producing = window
                .Where(p => p.SongName.ToString() == card.Song && p.ChartType == card.Type
                            && (int)p.Level == card.Level && p.Score != null && (int)p.Score.Value == card.Score
                            && p.RecordedAt != null)
                .OrderBy(p => p.RecordedAt)
                .FirstOrDefault();
            if (producing == null || card.At == null) continue;

            var same = producing.RecordedAt!.Value == card.At.Value;
            if (same) agree++;
            else disagree++;
            report.AppendLine(
                $"   {card.Song,-40} {card.Type}{card.Level,-3} {card.Score,10:N0}  card={card.At:MM-dd HH:mm:ss}  " +
                $"play={producing.RecordedAt:MM-dd HH:mm:ss}  {(same ? "same" : "DIFFERENT")}");
        }

        report.AppendLine($"   -> agree={agree} differ={disagree}");

        report.AppendLine();
        report.AppendLine("== every window play, with the best card for its chart");
        foreach (var play in window.OrderByDescending(p => p.RecordedAt))
        {
            var card = bests.FirstOrDefault(b => b.Song == play.SongName.ToString() && b.Type == play.ChartType
                                                 && b.Level == (int)play.Level);
            report.AppendLine(
                $"   {play.SongName,-40} {play.ChartType}{(int)play.Level,-3} " +
                $"play={(play.IsStageBroken || play.Score == null ? "STAGE BREAK" : $"{(int)play.Score.Value,10:N0}"),-12} " +
                $"at={play.RecordedAt:MM-dd HH:mm:ss}   card={(card.Song == null ? "(not on list)" : $"{card.Score,10:N0} @ {card.At:MM-dd HH:mm:ss}")}");
        }

        _output.WriteLine(report.ToString());
        Assert.NotEmpty(bests);
    }
}
