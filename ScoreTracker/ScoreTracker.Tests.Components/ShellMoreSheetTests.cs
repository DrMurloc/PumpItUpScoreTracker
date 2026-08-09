using System.Linq;
using AngleSharp.Dom;
using Bunit;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The phone's More sheet after the nav restructure. The sheet emits ONE grouped list
///     that CSS and nav.js present either as an icon grid or as a drill-down, so what is
///     worth pinning here is the list itself: which groups exist, in what order, and what is
///     in them. The layout swap is a viewport behaviour bUnit cannot see.
/// </summary>
public sealed class ShellMoreSheetTests : ComponentTestBase
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ShellViewModel Model(bool loggedIn = true,
        MixEnum mix = MixEnum.Phoenix, bool hasRecap = false,
        IReadOnlyList<TournamentRecord>? events = null)
    {
        return new ShellViewModel(
            IsLoggedIn: loggedIn,
            UserId: loggedIn ? UserId : null,
            DisplayName: loggedIn ? "alice" : null,
            AvatarUrl: "https://piu.test/avatar.png",
            CurrentMix: mix,
            ThemeMix: mix,
            HasRecap: hasRecap,
            HighlightedEvents: events ?? Array.Empty<TournamentRecord>(),
            ActivePath: "/",
            ReturnUrl: "/");
    }

    private IRenderedComponent<ShellMoreSheet> Render(ShellViewModel model)
    {
        return RenderComponent<ShellMoreSheet>(p => p.Add(c => c.Model, model));
    }

    private static string[] GroupNames(IRenderedComponent<ShellMoreSheet> sheet)
    {
        return sheet.FindAll("[data-more-group-head] span")
            .Select(e => e.TextContent.Trim())
            .ToArray();
    }

    private static string[] HrefsUnder(IRenderedComponent<ShellMoreSheet> sheet, string groupName)
    {
        var head = sheet.FindAll("[data-more-group-head]")
            .First(h => h.QuerySelector("span")!.TextContent.Trim() == groupName);
        var group = head.ParentElement!;
        return group.QuerySelectorAll(".more-group-body a")
            .Select(a => a.GetAttribute("href") ?? string.Empty)
            .ToArray();
    }

    private static string[] AllHrefs(IRenderedComponent<ShellMoreSheet> sheet)
    {
        return sheet.FindAll("a").Select(a => a.GetAttribute("href") ?? string.Empty).ToArray();
    }

    [Fact]
    public void GroupsRenderInTheSameOrderAsTheDesktopMenus()
    {
        var sheet = Render(Model());

        Assert.Equal(
            new[] { "My Progress", "Compete", "Leaderboards", "Community", "Tools" },
            GroupNames(sheet));
    }

    [Fact]
    public void ChartsLivesUnderToolsOnThePhone()
    {
        // Desktop keeps Charts in Play; here Tools is the catch-all. The sheet is the only
        // place the two nav trees deliberately disagree.
        var sheet = Render(Model());

        Assert.Contains("/Charts", HrefsUnder(sheet, "Tools"));
    }

    [Fact]
    public void LeaderboardsHoldsTheOfficialBoardsOnly()
    {
        var sheet = Render(Model());

        Assert.Equal(new[]
        {
            "/OfficialLeaderboards",
            "/OfficialLeaderboards/Rankings",
            "/OfficialLeaderboards/Players",
            "/OfficialLeaderboards/Popularity",
            "/OfficialLeaderboards/WhatItTakes"
        }, HrefsUnder(sheet, "Leaderboards"));
    }

    [Fact]
    public void RetiredDestinationsAreGone()
    {
        var sheet = Render(Model());

        var hrefs = AllHrefs(sheet);
        Assert.DoesNotContain("/ScoreRankings", hrefs);
        Assert.DoesNotContain("/Completion", hrefs);
        Assert.DoesNotContain("/UcsLeaderboards", hrefs);
    }

    [Fact]
    public void EveryDestinationIsEmittedExactlyOnce()
    {
        // Both layouts render from this one list. A destination appearing twice would mean
        // the markup had started to fork per layout, which is the thing this shape exists
        // to prevent.
        var sheet = Render(Model(hasRecap: true));

        var duplicated = AllHrefs(sheet).GroupBy(h => h)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(duplicated);
    }

    [Fact]
    public void TheDrillAffordancesShipInertForNavJsToPromote()
    {
        // Grid viewports never promote them, so they must carry no role or tab stop out of
        // the server — otherwise a keyboard user tabs onto five rows that do nothing.
        var sheet = Render(Model());

        Assert.All(sheet.FindAll("[data-more-group-head]"), head =>
        {
            Assert.False(head.HasAttribute("role"));
            Assert.False(head.HasAttribute("tabindex"));
        });
        Assert.True(sheet.Find("[data-more-back]").HasAttribute("hidden"));
    }

    [Fact]
    public void LoggedOutSheetDropsThePersonalRowsButKeepsTheRest()
    {
        var sheet = Render(Model(loggedIn: false));

        var hrefs = AllHrefs(sheet);
        Assert.DoesNotContain(hrefs, h => h.StartsWith("/Player/"));
        Assert.Contains("/Titles", hrefs);
        Assert.Contains("/WeeklyCharts", hrefs);
        Assert.Contains("/Charts", hrefs);
    }

    [Fact]
    public void RecapRowAppearsOnlyWhenThereIsARecap()
    {
        Assert.DoesNotContain(AllHrefs(Render(Model())), h => h.EndsWith("/PhoenixRecap"));
        Assert.Contains(AllHrefs(Render(Model(hasRecap: true))), h => h.EndsWith("/PhoenixRecap"));
    }

    [Fact]
    public void PumbilityHidesOnXxWhichHasNoSuchRating()
    {
        Assert.Contains("/Pumbility", AllHrefs(Render(Model(mix: MixEnum.Phoenix))));
        Assert.DoesNotContain("/Pumbility", AllHrefs(Render(Model(mix: MixEnum.XX))));
    }

    [Fact]
    public void HighlightedTournamentsJoinCompete()
    {
        var stamina = new TournamentRecord(Guid.NewGuid(), Name.From("Storm 2026"), 12,
            TournamentType.Stamina, "Online", IsHighlighted: true, LinkOverride: null,
            StartDate: null, EndDate: null, IsMoM: false);

        var sheet = Render(Model(events: new[] { stamina }));

        Assert.Contains($"/Tournament/Stamina/{stamina.Id}", HrefsUnder(sheet, "Compete"));
    }

    /// <summary>
    ///     A legacy mix reaches the same sheet as any other. Destinations used to be hidden
    ///     from the nav on pre-XX mixes, which made the site look smaller than it is and left
    ///     a player no way to find out why — a page that cannot answer for the mix in view
    ///     says so on arrival instead (docs/design/legacy-mixes.md).
    /// </summary>
    [Theory]
    [InlineData(MixEnum.XX)]
    [InlineData(MixEnum.Prime2)]
    [InlineData(MixEnum.FirstDanceFloor)]
    public void ALegacyMixSeesTheWholeSheet(MixEnum mix)
    {
        var sheet = Render(Model(mix: mix));

        Assert.Equal(new[] { "My Progress", "Compete", "Leaderboards", "Community", "Tools" },
            GroupNames(sheet));

        var hrefs = AllHrefs(sheet);
        Assert.Contains("/Charts", hrefs);
        Assert.Contains("/WeeklyCharts", hrefs);
        Assert.Contains("/Communities", hrefs);
        Assert.Contains("/PhoenixCalculator", hrefs);
        Assert.Contains("/About", hrefs);
    }

    /// <summary>
    ///     Import points at whichever importer the mix uses — scraping the official site on
    ///     Phoenix, a spreadsheet on everything older. It reached the sheet ONLY on a gated
    ///     mix before, so a Phoenix player on a phone had no import link at all.
    /// </summary>
    [Theory]
    [InlineData(MixEnum.Phoenix2, "/UploadPhoenixScores")]
    [InlineData(MixEnum.XX, "/UploadXXScores")]
    [InlineData(MixEnum.Prime2, "/UploadXXScores")]
    public void ImportPointsAtTheImporterThatMixUses(MixEnum mix, string expected)
    {
        Assert.Contains(expected, AllHrefs(Render(Model(mix: mix))));
    }

    [Fact]
    public void ImportIsOfferedOnlyToSignedInPlayers()
    {
        var hrefs = AllHrefs(Render(Model(loggedIn: false, mix: MixEnum.XX)));

        Assert.DoesNotContain("/UploadXXScores", hrefs);
        Assert.DoesNotContain("/UploadPhoenixScores", hrefs);
    }
}
