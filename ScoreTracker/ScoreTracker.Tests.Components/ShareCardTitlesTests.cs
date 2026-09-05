using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The header rule (design doc §9): title = what the rows are, subtitle = how to read them,
///     stamp = whose reading — or nothing when the title already said so.
/// </summary>
public sealed class ShareCardTitlesTests
{
    private static string L(string key) => key;

    [Fact]
    public void ATierViewNamesTheLensAndStampsWhoseReadingItIs()
    {
        var crowd = ShareCardTitles.TierList("Singles 20", ShareCardTitles.TierListView.Tier, "Pass Difficulty",
            false, "DrMurloc", null, "Phoenix", "2026-09-02", L);
        Assert.Equal("Singles 20 — Pass Difficulty", crowd.Title);
        Assert.Equal("Phoenix · 2026-09-02", crowd.Subtitle);
        Assert.Equal("Crowd sourced", crowd.Stamp);

        var personal = ShareCardTitles.TierList("Singles 20", ShareCardTitles.TierListView.Tier, "Score Difficulty",
            true, "DrMurloc", null, "Phoenix", "2026-09-02", L);
        Assert.Equal("Singles 20 — Score Difficulty", personal.Title);
        Assert.Equal("Personalized for DrMurloc", personal.Stamp);
    }

    [Fact]
    public void MyScoresLetsTheTitleOwnWhoseScoresAndDemotesTheLens()
    {
        var header = ShareCardTitles.TierList("Doubles 22", ShareCardTitles.TierListView.MyScores, "Pass Difficulty",
            false, "DrMurloc", "Age", "Phoenix 2", "2026-09-02", L);

        Assert.Equal("Doubles 22 — DrMurloc's Scores by Age", header.Title);
        Assert.Equal("Shown Difficulty: Pass Difficulty · Phoenix 2 · 2026-09-02", header.Subtitle);
        Assert.Null(header.Stamp);
    }

    [Fact]
    public void SpeedIsItsOwnSubject()
    {
        var header = ShareCardTitles.TierList("Singles 20", ShareCardTitles.TierListView.Speed, "Score Difficulty",
            false, "DrMurloc", null, "Phoenix", "2026-09-02", L);

        Assert.Equal("Singles 20 — Speed", header.Title);
        Assert.StartsWith("Shown Difficulty: Score Difficulty", header.Subtitle);
        Assert.Null(header.Stamp);
    }

    [Fact]
    public void TargetsPromoteTheGroupingAndPrintOnlyTheClarifiersThatApply()
    {
        var prevalence = ShareCardTitles.Targets(false, "Prevalence", "Great", "Singles pool",
            gainsOnly: true, phoenix1Projected: false, "Phoenix 2", "2026-09-02", "DrMurloc", L);
        Assert.Equal("PUMBILITY Targets — Prevalence", prevalence.Title);
        Assert.Equal("Energy: Great · Singles pool · Only projected PUMBILITY gains · Phoenix 2 · 2026-09-02",
            prevalence.Subtitle);
        Assert.Equal("Personalized for DrMurloc", prevalence.Stamp);

        var gains = ShareCardTitles.Targets(false, "Projected gains", "Good", null,
            gainsOnly: false, phoenix1Projected: true, "Phoenix", "2026-09-02", "DrMurloc", L);
        Assert.Equal("PUMBILITY Targets — Projected gains", gains.Title);
        Assert.Equal("Energy: Good · Phoenix 1 projected · Phoenix · 2026-09-02", gains.Subtitle);
    }

    [Fact]
    public void ThePoolLensIsThePoolNotATargetsList()
    {
        var pool = ShareCardTitles.Targets(true, "Your top 50", "Great", "Doubles pool",
            gainsOnly: false, phoenix1Projected: false, "Phoenix 2", "2026-09-02", "DrMurloc", L);

        Assert.Equal("PUMBILITY Pool — Top 50", pool.Title);
        Assert.Equal("Energy: Great · Doubles pool · Phoenix 2 · 2026-09-02", pool.Subtitle);
    }

    [Fact]
    public void TheBreakdownPagesFiftyNameThePageAndTheBlockAndCarryNoEnergy()
    {
        // PUMBILITY doc D57, §3.11: nothing on that page reads an Energy, so none rides the card.
        var pool = ShareCardTitles.Pool("Doubles pool", "Phoenix 2", "2026-09-05", "DrMurloc", L);
        Assert.Equal("PUMBILITY Breakdown — Your top 50", pool.Title);
        Assert.Equal("Doubles pool · Phoenix 2 · 2026-09-05", pool.Subtitle);
        Assert.Equal("Personalized for DrMurloc", pool.Stamp);

        var onePool = ShareCardTitles.Pool(null, "Phoenix", "2026-09-05", "DrMurloc", L);
        Assert.Equal("Phoenix · 2026-09-05", onePool.Subtitle);
    }

    [Fact]
    public void FileNamesCarryTheSubjectSoDownloadsOfOneFolderNeverCollide()
    {
        Assert.Equal("TierList_Phoenix_Single20_Pass_2026-09-02.png",
            ShareCardTitles.TierListFileName(MixEnum.Phoenix, ChartType.Single, 20, "Pass", "2026-09-02"));
        Assert.Equal("TierList_Phoenix_Single20_ScoresByScoreRanking_2026-09-02.png",
            ShareCardTitles.TierListFileName(MixEnum.Phoenix, ChartType.Single, 20, "ScoresBy Score Ranking",
                "2026-09-02"));
        Assert.Equal("PumbilityTargets_Phoenix2_Prevalence_Great_Single_2026-09-02.png",
            ShareCardTitles.TargetsFileName(MixEnum.Phoenix2, "Prevalence", "Great", "Single", "2026-09-02"));
        Assert.Equal("PumbilityTop50_Phoenix2_Single_2026-09-05.png",
            ShareCardTitles.PoolFileName(MixEnum.Phoenix2, "Single", "2026-09-05"));
    }
}
