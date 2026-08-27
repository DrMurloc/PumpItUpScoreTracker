using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
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
///     Renders the hero with every section populated. Razor validates unknown COMPONENTS at
///     compile time but not unknown PARAMETERS, so a mistyped one builds clean and throws on
///     first render — which is how a made-up SongImage "Size" reached a running page. Rendering
///     the whole tree once is what catches that class.
/// </summary>
public sealed class SessionHeroTests : ComponentTestBase
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

    // The hero can render a PatienceCard, which draws its flavour line through the RNG port.
    // Registered here rather than per test: bUnit seals its service provider the moment a
    // component asks it for anything.
    //
    // DifficultyBubble gates its tooltip on RendererInfo, and reading the renderer locks the
    // service collection — so RenderInteractive stays last.
    public SessionHeroTests()
    {
        Services.AddSingleton(new Mock<IRandomNumberGenerator>().Object);
        this.RenderInteractive();
    }

    [Fact]
    public void TheHeroRendersEverySectionItHasDataFor()
    {
        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, FullBreakdown()));

        Assert.NotEmpty(hero.FindAll("[data-testid='session-ceremony']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-title-bars']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-milestones']"));
        // Flagged scores render as cards (D41); the boards live in chart details now (D42).
        Assert.NotEmpty(hero.FindAll("[data-testid='session-highlights']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-highlight-card']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-all-plays']"));
    }

    [Fact]
    public void AnUnflaggedSessionKeepsTheCompactRowsAndNeverPadsWithCards()
    {
        // The pre-capture back catalogue: nothing flagged, so the hardest rows carry the
        // section — a card with nothing to say would be manufactured ceremony (D41).
        var breakdown = FullBreakdown();
        breakdown = breakdown with
        {
            Scores = breakdown.Scores.Select(s => s with { Flags = HighlightFlags.None, Detail = null })
                .ToArray()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        Assert.Empty(hero.FindAll("[data-testid='session-highlights']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-notable']"));
    }

    [Fact]
    public void APassAndItsLaterUpscoreMergeIntoOneCard()
    {
        // One chart, two captures across two batches: the card shows the final score and
        // carries both captures' marks — the pass's 🎯 belongs on it even though a later
        // upscore is the score it shows (D41).
        var chart = ChartAt(ChartType.Single, 22);
        var pass = Row(chart.Id, Start, 905000, ScoreEventClassification.NewPass);
        var upscore = Row(chart.Id, Start.AddHours(1), 931000, ScoreEventClassification.Upscore)
            with { PreviousBest = 905000 };
        var breakdown = FullBreakdown() with
        {
            Charts = new Dictionary<Guid, Chart> { [chart.Id] = chart },
            Scores = new[]
            {
                new SessionScore(pass, chart, HighlightFlags.FolderDebut,
                    new HighlightDetail(AttemptsBeforeClear: 3)),
                new SessionScore(upscore, chart, HighlightFlags.PumbilityTop50,
                    new HighlightDetail(PumbilityRank: 40, PeerCount: 80, PeerBetterCount: 7,
                        PeerPercentile: 0.9))
            }
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        var card = Assert.Single(hero.FindAll("[data-testid='session-highlight-card']"));
        Assert.Contains("931,000", card.TextContent);
        Assert.Contains("👑", card.TextContent);
        Assert.Contains("🎯 4", card.TextContent);
        // The gained score stands where the "Upscore" chip used to: number over label.
        Assert.Contains("+26,000", card.TextContent);
        Assert.DoesNotContain("Upscore", card.TextContent);
    }

    [Fact]
    public async Task TheJacketIsTheCardsOnlyWayIntoChartDetails()
    {
        // A whole-card target made the judgement strip a near-miss away from a navigation —
        // the jacket carries the click, the tier card's own affordance (field test).
        Chart? opened = null;
        var hero = RenderComponent<SessionHero>(p => p
            .Add(h => h.Breakdown, FullBreakdown())
            .Add(h => h.OnOpenChart, chart => opened = chart));

        await hero.Find("[data-testid='session-highlight-jacket']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(opened);
    }

    /// <summary>
    ///     The trophy is gone and the chart name carries the action. It used to be two controls
    ///     doing one job — a name that navigated to the chart page and a trophy that opened a
    ///     board — and the details dialogue now holds the board itself.
    /// </summary>
    [Fact]
    public void TheChartNameOpensTheDetailsDialogueAndNoTrophyRemains()
    {
        Chart? opened = null;
        var hero = RenderComponent<SessionHero>(p => p
            .Add(h => h.Breakdown, FullBreakdown())
            .Add(h => h.OnOpenChart, chart => opened = chart));

        Assert.Empty(hero.FindAll("[data-testid='session-row-leaderboard']"));
        var link = hero.FindAll("[data-testid='session-row-chart']");
        Assert.NotEmpty(link);

        link[0].Click();

        Assert.NotNull(opened);
    }

    [Fact]
    public void AScoreWithNoCohortRendersWithoutAStanding()
    {
        // Co-op and far-below-competitive charts have nothing measuring them. The row prints no
        // standing and says nothing about why — a disclaimer there confuses more than it helps.
        // The measured row alongside it proves the absence is the cohort's, not the render's.
        var breakdown = FullBreakdown();
        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        // 12 peers scored higher, so the place is 13th; the denominator is the whole cohort.
        Assert.Contains("#13 of 74 peers", hero.Markup);
        Assert.DoesNotContain("cohort has no", hero.Markup);
    }

    [Fact]
    public void ABreakIsNeverAScoreThatMattered()
    {
        // The section tops itself up with unflagged rows to fill six, so on a thin session the
        // highest-level row can be a stage break — which is exactly the thing this page never
        // highlights (D6). It belongs in All plays and nowhere else.
        var breakChart = ChartAt(ChartType.Single, 26);
        var breakRow = Row(breakChart.Id, Start.AddMinutes(20), 794532, ScoreEventClassification.Break);
        var breakdown = FullBreakdown();
        var withBreak = breakdown with
        {
            Charts = new Dictionary<Guid, Chart>(breakdown.Charts) { [breakChart.Id] = breakChart },
            Scores = breakdown.Scores
                .Append(new SessionScore(breakRow with { IsBroken = true }, breakChart,
                    HighlightFlags.None, null))
                .ToArray()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, withBreak));

        var highlights = hero.Find("[data-testid='session-highlights']");
        Assert.DoesNotContain("794,532", highlights.InnerHtml);
        // Still present in the neutral log, where the record lives.
        Assert.Contains("794,532", hero.Find("[data-testid='session-all-plays']").InnerHtml);
    }

    [Fact]
    public void FivePlaysOfOneChartThreadAndCaptionTheirAttempts()
    {
        // The rail joins the run; captions start at five plays (D49) and skip the newest row —
        // its badges tell the ending, and numbering it would caption the punchline.
        var chart = ChartAt(ChartType.Double, 23);
        var rows = Enumerable.Range(0, 5)
            .Select(i => Row(chart.Id, Start.AddMinutes(i * 7), 900000 + i * 5000,
                ScoreEventClassification.Upscore))
            .ToArray();
        var breakdown = FullBreakdown() with
        {
            Charts = new Dictionary<Guid, Chart> { [chart.Id] = chart },
            Scores = rows.Select(r => new SessionScore(r, chart, HighlightFlags.None, null)).ToArray()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        Assert.Equal(5, hero.FindAll(".sbd-thread").Count);
        Assert.Single(hero.FindAll(".sbd-thread-start"));
        Assert.Single(hero.FindAll(".sbd-thread-end"));
        // Four captions for five plays: attempts 1–4, the newest uncounted.
        var captions = hero.FindAll("[data-testid='session-row-attempt']");
        Assert.Equal(4, captions.Count);
        Assert.Contains("Attempt 4", hero.Markup);
        Assert.Contains("Attempt 1", hero.Markup);
        Assert.DoesNotContain("Attempt 5", hero.Markup);
    }

    [Fact]
    public void TwoPlaysOfOneChartThreadWithoutCaptions()
    {
        // The rail is cheap annotation; a number on a two-try chart is noise (D49).
        var chart = ChartAt(ChartType.Single, 20);
        var rows = new[]
        {
            Row(chart.Id, Start, 910000, ScoreEventClassification.NewPass),
            Row(chart.Id, Start.AddMinutes(30), 931000, ScoreEventClassification.Upscore)
        };
        var breakdown = FullBreakdown() with
        {
            Charts = new Dictionary<Guid, Chart> { [chart.Id] = chart },
            Scores = rows.Select(r => new SessionScore(r, chart, HighlightFlags.None, null)).ToArray()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        Assert.Equal(2, hero.FindAll(".sbd-thread").Count);
        Assert.Empty(hero.FindAll("[data-testid='session-row-attempt']"));
    }

    [Fact]
    public void APendingCaptureStandsInForEverythingItWouldHaveFilled()
    {
        // Capture runs on the bus behind the import, so the page can beat it there. One card
        // replaces the whole region rather than four sections each rendering empty beside it.
        var pending = FullBreakdown() with
        {
            CaptureWindowOpen = true, CapturedRows = 0,
            Milestones = Array.Empty<PlayerMilestoneRecord>(),
            TitleBars = Array.Empty<SessionTitleBarModel>()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, pending));

        Assert.NotEmpty(hero.FindAll("[data-testid='session-capture-pending']"));
        Assert.Empty(hero.FindAll("[data-testid='session-highlights']"));
        Assert.Empty(hero.FindAll("[data-testid='session-notable']"));
    }

    [Fact]
    public void APendingCaptureKeepsTheNumbersThatDoNotComeFromIt()
    {
        // The band reads your stats row and All plays reads the journal — both true the moment
        // the import lands. Hiding them would take real data away to explain one absence.
        var pending = FullBreakdown() with { CaptureWindowOpen = true, CapturedRows = 0 };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, pending));

        Assert.NotEmpty(hero.FindAll("[data-testid='session-ceremony']"));
        Assert.NotEmpty(hero.FindAll("[data-testid='session-all-plays']"));
    }

    [Fact]
    public void AQuietSessionStillRendersTheCeremonyBand()
    {
        // The band is the anchor: a session that moved nothing keeps the shape rather than
        // collapsing into an empty hero.
        var quiet = FullBreakdown() with
        {
            Ceremony = new SessionCeremony(64612, null, null, null, null, null, null, 22.6, 23.4, null, null, null),
            TitleBars = Array.Empty<SessionTitleBarModel>(),
            Milestones = Array.Empty<PlayerMilestoneRecord>()
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, quiet));

        Assert.NotEmpty(hero.FindAll("[data-testid='session-ceremony']"));
        Assert.Empty(hero.FindAll("[data-testid='session-title-bars']"));
    }

    /// <summary>
    ///     A P2 gain that crossed a rung reads as the crossing, with the raw numbers demoted to
    ///     the sub-line (docs/design/pumbility-levels.md §5).
    /// </summary>
    [Fact]
    public void APumbilityGainThatCrossedARungReadsAsALevelUp()
    {
        var breakdown = FullBreakdown();
        breakdown = breakdown with
        {
            Group = breakdown.Group with { Mix = MixEnum.Phoenix2 },
            Milestones = new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, Session, Start, 17_410.38, 17_602.69,
                    null, null)
            }
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        var strip = hero.Find("[data-testid='milestone-strip']");
        Assert.Contains("Level Up", strip.TextContent);
        Assert.Contains("DIAMOND LV.3 → LV.4", strip.TextContent);
        // The raw gain stays on the strip as the sub-line (N0 rounds nearest: .69 goes up).
        Assert.Contains("17,410 → 17,603", strip.TextContent);
    }

    [Fact]
    public void TheGemTitleOutranksTheLevelStripInItsOwnBatch()
    {
        // Crossing into RED BERYL LV.1 IS the [P.B] RED BERYL title. When the batch completed it,
        // the title strip is the sentence and the gain renders in its plain form.
        var breakdown = FullBreakdown();
        breakdown = breakdown with
        {
            Group = breakdown.Group with { Mix = MixEnum.Phoenix2 },
            Milestones = new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, Session, Start, 17_950, 18_010,
                    null, null),
                new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, Session, Start, null, null,
                    "[P.B] RED BERYL", null)
            }
        };

        var hero = RenderComponent<SessionHero>(p => p.Add(h => h.Breakdown, breakdown));

        var strips = string.Join(" | ", hero.FindAll("[data-testid='milestone-strip']")
            .Select(s => s.TextContent));
        Assert.DoesNotContain("Level Up", strips);
        Assert.Contains("PUMBILITY", strips);
        Assert.Contains("[P.B] RED BERYL", strips);
    }

    private static SessionBreakdown FullBreakdown()
    {
        var single = ChartAt(ChartType.Single, 21);
        var coOp = ChartAt(ChartType.CoOp, 18);
        var rows = new[]
        {
            Row(single.Id, Start, 912400, ScoreEventClassification.NewPass),
            Row(coOp.Id, Start.AddMinutes(9), 876300, ScoreEventClassification.NewPass)
        };
        var charts = new Dictionary<Guid, Chart> { [single.Id] = single, [coOp.Id] = coOp };

        var scores = new[]
        {
            new SessionScore(rows[0], single, HighlightFlags.FolderDebut | HighlightFlags.OfficialBoardPlacement,
                new HighlightDetail(FolderDebutOrdinal: 1, PeerCount: 74, PeerBetterCount: 12,
                    PeerPercentile: 0.84, AttemptsBeforeClear: 6, OfficialPlace: 42, OfficialBoardDepth: 100,
                    OfficialAsOf: Start.AddDays(-6))),
            // No detail at all: the co-op row is the one with nothing measuring it.
            new SessionScore(rows[1], coOp, HighlightFlags.None, null)
        };

        return new SessionBreakdown(
            new RecentSessionsPage.SessionGroup(Session, null, MixEnum.Phoenix, "officialImport",
                Start, Start.AddMinutes(9), rows),
            new ScoreSessionRecord(Session, Guid.NewGuid(), MixEnum.Phoenix, "officialImport",
                "DRMURLOC #7251", "01", Start, Start.AddMinutes(9), 2, 2, 0),
            charts, scores,
            new SessionCeremony(64612, 64466, 64612, 22.62, 22.68, null, null, 22.68, 23.4, 131, 148,
                Start.AddDays(-6)),
            new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, Session, Start, null, null,
                    "Advanced Lv. 3", null)
            },
            new[] { new SessionTitleBarModel("21", "Advanced Lv. 4", 0.61, 0.78, 3120, 4000) });
    }

    private static Chart ChartAt(ChartType type, int level)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null);
    }

    private static RecentSessionsPage.ScoreEventRecord Row(Guid chartId, DateTimeOffset at, int score,
        ScoreEventClassification classification)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, at, score, "Fair Game", false, "seed",
            Session, classification, null);
    }
}
