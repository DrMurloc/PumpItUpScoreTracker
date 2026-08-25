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
    public void TheImproverReadoutCarriesWhatTheScoreRatedAndHowFarOverAndTheArrowIsGone()
    {
        // A Single at level 21 scoring 985,000 rates 21 + (985000-965000)/17500 = 22.14, then
        // the Singles multiplier for 20+ takes it to 22.5. Against a baseline of 22.2 that is
        // +0.3 — and both halves come from one stored number plus a pure function. The ⬆ glyph
        // retired with D47: the readout says the same fact as a number.
        var row = Render(Score(985000, new HighlightDetail(CompetitiveBaseline: 22.2),
            HighlightFlags.CompetitiveImprover));

        Assert.Contains("22.5 (+0.3)", row.Markup);
        Assert.DoesNotContain("⬆", row.Markup);
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
    public void WithoutACapturedBaselineTheImproverShowsNothing()
    {
        // Pre-baseline rows used to render a bare ⬆; with the glyph retired (D47) the flag has
        // no visible form here — it still rides the model and the Discord card.
        var row = Render(Score(985000, null, HighlightFlags.CompetitiveImprover));

        Assert.DoesNotContain("⬆", row.Markup);
        Assert.DoesNotContain("(+", row.Markup);
    }

    [Fact]
    public async Task ObservedJudgementsRenderAsTheStripAndOpenTheBreakdown()
    {
        SessionScore? opened = null;
        var score = Score(985000, null);
        score = score with { Row = score.Row with { Judgements = new JudgementCounts(731, 74, 11, 4, 18, 214) } };
        var row = RenderComponent<SessionScoreRow>(p => p
            .Add(r => r.Score, score)
            .Add(r => r.OnOpenBreakdown, s => opened = s));

        Assert.Contains("731", row.Markup);
        await row.Find("button.judg-strip").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(opened);
    }

    [Fact]
    public void APlayWithoutObservedJudgementsSaysNothingAboutThem()
    {
        var row = Render(Score(985000, null));

        Assert.Empty(row.FindAll(".judg-strip"));
    }

    [Fact]
    public void AStageBreaksStripIsAReadoutNotAWayIn()
    {
        // The counts are real and render; the breakdown is a story against 1,000,000 that a
        // partial run does not have, so the strip has nowhere to go (D44).
        var stageBreak = StageBreak(judgedNotes: 362, chartNoteCount: 1163);
        stageBreak = stageBreak with
        {
            Row = stageBreak.Row with { Judgements = new JudgementCounts(300, 40, 9, 6, 7) }
        };
        var row = RenderComponent<SessionScoreRow>(p => p
            .Add(r => r.Score, stageBreak)
            .Add(r => r.OnOpenBreakdown, _ => { }));

        Assert.NotEmpty(row.FindAll("span.judg-strip"));
        Assert.Empty(row.FindAll("button.judg-strip"));
    }

    [Fact]
    public void APerfectGameAlwaysGlowsEvenWhenTheWholeCohortSharesIt()
    {
        // D46 as reversed at the field test: 1,000,000 cannot be beaten, so the glow is the
        // achievement's own rather than a rarity claim. The standing line keeps the honest
        // shared-PG fact alongside it.
        var row = Render(Score(PerfectGame, new HighlightDetail(PeerCount: 94, PeerBetterCount: 0,
            PeerPgCount: 60, PeerPercentile: 1.0)));

        Assert.Contains("rarity-glow-3", row.Markup);
        Assert.Contains("59 of 93 peers have it", row.Markup);
    }

    [Fact]
    public void APerfectGameBelowTheCaptureFloorStillGlows()
    {
        // Capture skips charts far under competitive, so a PG there carries no detail at all —
        // under the percentile rule it glowed nothing, which read as a bug on the owner's own
        // low-chart PGs.
        var row = Render(Score(PerfectGame, null));

        Assert.Contains("rarity-glow-3", row.Markup);
    }

    [Fact]
    public void ABrokenRowNeverPrintsAStanding()
    {
        // Whatever detail reaches a broken row describes a score the run never achieved —
        // pinning keeps detail off these rows, and the row itself agrees.
        var broken = Score(400000, new HighlightDetail(PeerCount: 94, PeerBetterCount: 12,
            PeerPercentile: 0.4));
        broken = broken with { Row = broken.Row with { IsBroken = true, Classification = ScoreEventClassification.Break } };

        var row = Render(broken);

        Assert.DoesNotContain("peers", row.Markup);
    }

    [Fact]
    public void TheAttemptCaptionRendersOnlyWhenSupplied()
    {
        var without = Render(Score(912000, null));
        var with5 = RenderComponent<SessionScoreRow>(p => p
            .Add(r => r.Score, Score(912000, null))
            .Add(r => r.AttemptNumber, (int?)3));

        Assert.Empty(without.FindAll("[data-testid=session-row-attempt]"));
        Assert.Contains("Attempt 3", with5.Markup);
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

    [Fact]
    public void AStageBreakSaysSoAndHowFarTheRunGot()
    {
        // 362 notes judged of 1,163: the row prints the phrase and the share where a grade, plate
        // and number would sit — no grade image, no number, no glow.
        var row = Render(StageBreak(judgedNotes: 362, chartNoteCount: 1163));

        Assert.Contains("Stage break · 31% in", row.Markup);
        Assert.Contains("session-row-stage-break", row.Markup);
        Assert.DoesNotContain("piuimages.arroweclip.se/letters", row.Markup);
        Assert.DoesNotContain("sbd-score", row.Markup);
    }

    [Fact]
    public void AStageBreakWithNoBreakdownOrNoNoteCountKeepsThePlainPhrase()
    {
        // A best-list stage break carries no breakdown; a chart nobody has passed carries no count.
        // Either way the row says what it knows and invents no figure.
        var noBreakdown = Render(StageBreak(judgedNotes: null, chartNoteCount: 1163));
        var noCount = Render(StageBreak(judgedNotes: 362, chartNoteCount: null));

        Assert.Contains("Stage break", noBreakdown.Markup);
        Assert.DoesNotContain("% in", noBreakdown.Markup);
        Assert.Contains("Stage break", noCount.Markup);
        Assert.DoesNotContain("% in", noCount.Markup);
    }

    private IRenderedComponent<SessionScoreRow> Render(SessionScore score)
    {
        return RenderComponent<SessionScoreRow>(p => p.Add(r => r.Score, score));
    }

    private static SessionScore StageBreak(int? judgedNotes, int? chartNoteCount)
    {
        var song = new Song("Arcana Force", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2, song, ChartType.Double,
            DifficultyLevel.From(20), MixEnum.Phoenix2, null, chartNoteCount, new HashSet<Skill>());
        var row = new RecentSessionsPage.ScoreEventRecord(chart.Id, Start, null, null, true,
            "officialImport", Session, ScoreEventClassification.Played, null, false, true, judgedNotes);
        return new SessionScore(row, chart, HighlightFlags.None, null);
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
