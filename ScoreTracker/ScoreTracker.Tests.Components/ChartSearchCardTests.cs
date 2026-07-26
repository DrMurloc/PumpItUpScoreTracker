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
                true, false, DateTimeOffset.Parse("2026-06-01T00:00:00Z")));

        var unplayedCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, unplayed).Add(x => x.ShowMyState, true));
        var playedCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, played).Add(x => x.ShowMyState, true));

        Assert.Contains("Unplayed", unplayedCut.Markup);
        Assert.Contains("912,447", playedCut.Markup);
        Assert.Contains("TG", playedCut.Markup);
    }

    [Fact]
    public void ALegacyGradeRendersAsLetterArtNotBareText()
    {
        // "D ⨯" read as "D x" and meant nothing. XX grades borrow the Phoenix letter art
        // (every letter XX uses exists there) until the XX set is drawn.
        var result = ChartsPageTests.MakeResult("Turkey March", 6, MixEnum.Prex3,
            my: new ChartSearchMyState(null, null, null, XXLetterGrade.D, 88000, true,
                true, DateTimeOffset.Parse("2026-06-01T00:00:00Z")));

        var cut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, result).Add(x => x.ShowMyState, true));

        var src = cut.Find(".srp-card-my img").GetAttribute("src")!;
        Assert.Contains("/letters/d_broken.png", src);
        Assert.DoesNotContain("⨯", cut.Markup);
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
    public void TheStateBorderLanguagePutsPassedAheadOfToDo()
    {
        var passed = ChartsPageTests.MakeResult("Passed", 19,
            my: new ChartSearchMyState(950000, PhoenixLetterGrade.AAA, null, null, null, false,
                true, DateTimeOffset.Parse("2026-06-01T00:00:00Z")));
        var todoOnly = ChartsPageTests.MakeResult("Saved", 19);

        var passedCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, passed).Add(x => x.IsToDo, true).Add(x => x.ShowMyState, true));
        var todoCut = RenderComponent<ChartSearchCard>(p => p
            .Add(x => x.Result, todoOnly).Add(x => x.IsToDo, true).Add(x => x.ShowMyState, true));

        // Passed outranks To-Do when a chart is both.
        Assert.NotNull(passedCut.Find(".srp-card-pass"));
        Assert.Empty(passedCut.FindAll(".srp-card-todo"));
        Assert.NotNull(todoCut.Find(".srp-card-todo"));
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
