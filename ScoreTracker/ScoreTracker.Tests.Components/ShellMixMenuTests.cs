using System.Linq;
using Bunit;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The mix switcher's primary rows: newest first with the Rise pair between the two Phoenix
///     generations, each led by its game's wordmark; the legacy rows behind "More Mixes" stay text
///     (owner, 2026-09-22).
/// </summary>
public sealed class ShellMixMenuTests : ComponentTestBase
{
    private static ShellViewModel Model(MixEnum mix = MixEnum.Phoenix2)
    {
        return new ShellViewModel(
            IsLoggedIn: true,
            UserId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DisplayName: "alice",
            AvatarUrl: "https://piu.test/avatar.png",
            CurrentMix: mix,
            ThemeMix: mix,
            HasRecap: false,
            HighlightedEvents: Array.Empty<TournamentRecord>(),
            ActivePath: "/",
            ReturnUrl: "/");
    }

    [Fact]
    public void ThePrimaryRowsRunPhoenix2RiseRiseArcadePhoenixXx()
    {
        var cut = RenderComponent<ShellMixMenu>(p => p.Add(c => c.Model, Model()));

        var rows = cut.FindAll("[data-menu-panel] > a.shell-menu-item");
        Assert.Equal(new[] { "Phoenix2", "Rise", "RiseArcade", "Phoenix", "XX" },
            rows.Select(a => a.GetAttribute("href")!.Split("mix=")[1].Split('&')[0]).ToArray());
        Assert.Equal(new[] { "Phoenix 2", "Rise", "Rise Arcade", "Phoenix", "XX" },
            rows.Select(a => a.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void EveryPrimaryRowLeadsWithItsWordmarkAndTheArcadeStationWearsRises()
    {
        var cut = RenderComponent<ShellMixMenu>(p => p.Add(c => c.Model, Model()));

        var logos = cut.FindAll("[data-menu-panel] > a.shell-menu-item img.mix-row-logo")
            .Select(i => i.GetAttribute("src")).ToArray();
        Assert.Equal(new[]
        {
            "/img/mixes/phoenix2.png", "/img/mixes/rise.png", "/img/mixes/rise.png", "/img/mixes/phoenix.png",
            "/img/mixes/xx.png"
        }, logos);
    }

    [Fact]
    public void LegacyRowsStayText()
    {
        var cut = RenderComponent<ShellMixMenu>(p => p.Add(c => c.Model, Model()));

        var legacyRows = cut.FindAll("details a.shell-menu-item");
        Assert.NotEmpty(legacyRows);
        Assert.All(legacyRows, a => Assert.Empty(a.QuerySelectorAll("img")));
    }
}
