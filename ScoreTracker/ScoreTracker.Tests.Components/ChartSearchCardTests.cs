using System;
using System.Collections.Generic;
using Bunit;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The SRP result card: one stretched link to the chart page, granular badge chips,
///     the family-forked tier chip (Community Vote for legacy, Pass Difficulty for
///     modern), and the auto-surfaced lead fact.
/// </summary>
public sealed class ChartSearchCardTests : ComponentTestBase
{
    public ChartSearchCardTests()
    {
        // DifficultyBubble branches on RendererInfo (the MudTooltip static-SSR gate).
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    [Fact]
    public void BadgesRenderTheirDisplayNamesWithCategoryTint()
    {
        var result = ChartsPageTests.MakeResult("District 1", 21, badges: new[]
        {
            new ChartBadge("staggered_bracket", "Staggered Brackets", SkillCategory.Bracket),
            new ChartBadge("doublestep", "Doublesteps", null)
        });

        var cut = RenderComponent<ChartSearchCard>(p => p.Add(x => x.Result, result));

        Assert.Contains("Staggered Brackets", cut.Markup);
        Assert.Single(cut.FindAll(".skillcat-bracket"));
        Assert.Contains("Doublesteps", cut.Markup);
    }

    [Fact]
    public void LegacyResultsWearTheCommunityVoteChipModernOnesThePassChip()
    {
        var legacy = ChartsPageTests.MakeResult("Turkey March", 6, MixEnum.Prex3,
            communityVote: TierListCategory.VeryHard);
        var modern = ChartsPageTests.MakeResult("Bee", 23, communityVote: null,
            passDifficulty: TierListCategory.Hard);

        var legacyCut = RenderComponent<ChartSearchCard>(p => p.Add(x => x.Result, legacy));
        var modernCut = RenderComponent<ChartSearchCard>(p => p.Add(x => x.Result, modern));

        Assert.Contains("Community Vote", legacyCut.Markup);
        Assert.Contains("Very Hard", legacyCut.Markup);
        Assert.DoesNotContain("Community Vote", modernCut.Markup);
        Assert.Contains("Pass", modernCut.Markup);
    }

    [Fact]
    public void SignedInVisitorsSeeUnplayedUntilARecordExists()
    {
        var unplayed = ChartsPageTests.MakeResult("Bee", 23);
        var played = ChartsPageTests.MakeResult("District 1", 21,
            my: new ChartSearchMyState(912447, PhoenixLetterGrade.A, PhoenixPlate.TalentedGame, null, null,
                true, DateTimeOffset.Parse("2026-06-01T00:00:00Z"), false, false));

        var unplayedCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, unplayed).Add(x => x.ShowMyState, true));
        var playedCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, played).Add(x => x.ShowMyState, true));

        Assert.Contains("Unplayed", unplayedCut.Markup);
        Assert.Contains("912,447", playedCut.Markup);
        Assert.Contains("TG", playedCut.Markup);
    }

    [Fact]
    public void TheLeadFactRendersAheadOfTheStandingFacts()
    {
        var result = ChartsPageTests.MakeResult("District 1", 21);

        var cut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, result).Add(x => x.LeadFact, "Scoring level 21.4"));

        Assert.Equal("Scoring level 21.4", cut.Find(".srp-card-fact-lead").TextContent);
    }

    [Fact]
    public void MultiMixIdentitiesRenderTheSpanWithTheRerateArrow()
    {
        var result = ChartsPageTests.MakeResult("Beethoven Virus", 19) with
        {
            Appearances = new[]
            {
                new ChartMixAppearance(MixEnum.Exceed, 18, null),
                new ChartMixAppearance(MixEnum.Phoenix, 19, null)
            },
            DebutMix = MixEnum.PerfectCollection,
            LatestMix = MixEnum.Phoenix,
            LevelChange = 1
        };

        var cut = RenderComponent<ChartSearchCard>(p => p.Add(x => x.Result, result));

        var span = cut.Find(".srp-card-span").TextContent;
        Assert.Contains("The Perfect Collection", span);
        Assert.Contains("2 mixes", span);
        Assert.Contains("18 → 19", cut.Find(".srp-rerate-up").TextContent);
    }

    [Fact]
    public void SingleMixResultsSkipTheSpanLine()
    {
        var cut = RenderComponent<ChartSearchCard>(p => p.Add(x => x.Result,
            ChartsPageTests.MakeResult("Bee", 23)));

        Assert.Empty(cut.FindAll(".srp-card-span"));
    }

    [Fact]
    public void TheStateBorderLanguageFollowsPassedThenToDoThenOtherMix()
    {
        var passed = ChartsPageTests.MakeResult("Passed", 19,
            my: new ChartSearchMyState(950000, PhoenixLetterGrade.AAA, null, null, null, false,
                DateTimeOffset.Parse("2026-06-01T00:00:00Z"), true, false));
        var otherMix = ChartsPageTests.MakeResult("Elsewhere", 19,
            my: new ChartSearchMyState(null, null, null, null, null, false, null, false, true));

        var passedCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, passed).Add(x => x.IsToDo, true).Add(x => x.ShowMyState, true));
        var otherCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, otherMix).Add(x => x.ShowMyState, true));

        // Passed outranks To-Do; the cross-mix pass wears the dashed-green language + chip.
        Assert.NotNull(passedCut.Find(".srp-card-pass"));
        Assert.Empty(passedCut.FindAll(".srp-card-todo"));
        Assert.NotNull(otherCut.Find(".srp-card-other-mix"));
        Assert.Contains("Passed in another mix", otherCut.Markup);
    }

    [Fact]
    public void TheWholeCardIsOneLinkToTheChartPage()
    {
        var result = ChartsPageTests.MakeResult("District 1", 21);

        var cut = RenderComponent<ChartSearchCard>(p => p.Add(x => x.Result, result));

        var href = cut.Find(".srp-card-link").GetAttribute("href");
        Assert.StartsWith("/Charts/phoenix/district-1/", href);
    }
}
