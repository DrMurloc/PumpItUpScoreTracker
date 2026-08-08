using System;
using System.Collections.Generic;
using Bunit;
using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     One score, and what the row says about it. Standing reads as a PLACE rather than a share
///     of the cohort — the same fact the Discord card already prints, in the same words.
/// </summary>
public sealed class SessionScoreRowTests : ComponentTestBase
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);
    private const int PerfectGame = 1_000_000;

    public SessionScoreRowTests() => this.RenderInteractive();

    [Fact]
    public void StandingIsAPlaceInsideTheWholeCohort()
    {
        // Twelve peers scored higher, so you are thirteenth. The denominator keeps everyone,
        // yourself included: a place is a position inside a population you belong to.
        var row = Render(Score(972000, new HighlightDetail(PeerCount: 94, PeerBetterCount: 12,
            PeerPercentile: 0.87)));

        Assert.Contains("#13 of 94 peers", row.Markup);
    }

    [Fact]
    public void TheBestScoreInTheCohortIsFirstRatherThanATopZeroPercent()
    {
        var row = Render(Score(998120, new HighlightDetail(PeerCount: 94, PeerBetterCount: 0,
            PeerPercentile: 1.0)));

        Assert.Contains("#1 of 94 peers", row.Markup);
        Assert.DoesNotContain("%", row.Markup);
    }

    [Fact]
    public void APerfectGameCountsWhoElseHoldsItRatherThanRankingEveryoneFirst()
    {
        // Four hold the PG including you, out of a 94-strong cohort. Both numbers drop one:
        // "peers" is the other people at your level.
        var row = Render(Score(PerfectGame, new HighlightDetail(PeerCount: 94, PeerBetterCount: 0,
            PeerPgCount: 4, PeerPercentile: 1.0)));

        Assert.Contains("3 of 93 peers have it", row.Markup);
    }

    [Fact]
    public void ALonePerfectGameFallsBackToThePlace()
    {
        // "0 of 93 peers have it" is a worse way of saying first, so the place says it instead.
        var row = Render(Score(PerfectGame, new HighlightDetail(PeerCount: 94, PeerBetterCount: 0,
            PeerPgCount: 1, PeerPercentile: 1.0)));

        Assert.Contains("#1 of 94 peers", row.Markup);
        Assert.DoesNotContain("peers have it", row.Markup);
    }

    [Fact]
    public void AScoreWithNoCohortSaysNothingAboutStanding()
    {
        var row = Render(Score(876300, null));

        Assert.DoesNotContain("peers", row.Markup);
    }

    [Fact]
    public void TheScoreQualityBadgeIsGoneNowThatThePlacePrintsBeneathIt()
    {
        // The flag still exists and still rides the Discord card; the glyph said "top 10% among
        // comparable players", which is what the standing line now states outright.
        var row = Render(Score(972000, new HighlightDetail(PeerCount: 94, PeerBetterCount: 4,
                PeerPercentile: 0.96),
            HighlightFlags.ScoreQuality90));

        Assert.DoesNotContain("📊", row.Markup);
        Assert.Contains("#5 of 94 peers", row.Markup);
    }

    [Fact]
    public void PassingYourPhoenix1BestPrintsHowFarPast()
    {
        var row = Render(Score(981450, null) with { Phoenix1Gain = 27450 });

        Assert.Contains("+27,450 over P1", row.Markup);
    }

    [Fact]
    public void AScoreThatDidNotPassPhoenix1SaysNothingAboutIt()
    {
        var row = Render(Score(981450, null));

        Assert.DoesNotContain("over P1", row.Markup);
    }

    [Fact]
    public void TheImproverArrowCarriesWhatTheScoreRatedAndHowFarOver()
    {
        // A Single at level 21 scoring 985,000 rates 21 + (985000-965000)/17500 = 22.14, then
        // the Singles multiplier for 20+ takes it to 22.5. Against a baseline of 22.2 that is
        // +0.3 — and both halves come from one stored number plus a pure function.
        var row = Render(Score(985000, new HighlightDetail(CompetitiveBaseline: 22.2),
            HighlightFlags.CompetitiveImprover));

        Assert.Contains("22.5 (+0.3)", row.Markup);
    }

    [Fact]
    public void MovementUnderATwentiethOfALevelIsNotWorthSaying()
    {
        // "+0.0" is recomputation noise wearing a plus sign.
        var row = Render(Score(985000, new HighlightDetail(CompetitiveBaseline: 22.48),
            HighlightFlags.CompetitiveImprover));

        Assert.DoesNotContain("(+", row.Markup);
    }

    [Fact]
    public void WithoutACapturedBaselineTheArrowStaysBare()
    {
        // Every session before this capture landed. The arrow still means what it always did.
        var row = Render(Score(985000, null, HighlightFlags.CompetitiveImprover));

        Assert.Contains("⬆", row.Markup);
        Assert.DoesNotContain("(+", row.Markup);
    }

    [Fact]
    public void APumbilityGainRidesBesideTheCrownItExplains()
    {
        var row = Render(Score(972000, new HighlightDetail(PumbilityRank: 12, PumbilityGain: 112),
            HighlightFlags.PumbilityTop50));

        Assert.Contains("+112", row.Markup);
        Assert.Contains("sbd-gain", row.Markup);
    }

    [Fact]
    public void AChartInThePoolThatGainedNothingWearsNoBadge()
    {
        // The crown is a standing fact — a chart can sit in your top 50 all night having added
        // nothing. That is the whole reason the gain is captured separately.
        var row = Render(Score(972000, new HighlightDetail(PumbilityRank: 12),
            HighlightFlags.PumbilityTop50));

        Assert.Contains("👑", row.Markup);
        Assert.DoesNotContain("sbd-gain", row.Markup);
    }

    private IRenderedComponent<SessionScoreRow> Render(SessionScore score)
    {
        return RenderComponent<SessionScoreRow>(p => p.Add(r => r.Score, score));
    }

    private static SessionScore Score(int score, HighlightDetail? detail,
        HighlightFlags flags = HighlightFlags.None)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2, song, ChartType.Single,
            DifficultyLevel.From(21), MixEnum.Phoenix2, null, null, new HashSet<Skill>());
        var row = new RecentSessionsPage.ScoreEventRecord(chart.Id, Start, score, "Fair Game", false,
            "officialImport", Session, ScoreEventClassification.Upscore, score - 4000);
        return new SessionScore(row, chart, flags, detail);
    }
}
