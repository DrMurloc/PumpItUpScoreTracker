using System;
using Bunit;
using Microsoft.AspNetCore.Components;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Pages.Competition.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The March of Murlocs section chrome: real-link chips in the PUMBILITY frame's idiom, a
///     static Past-seasons button the island answers through mom-seasons.js, and a Record chip
///     only where there is a board to record on.
/// </summary>
public sealed class MoMSectionFrameTests : ComponentTestBase
{
    public MoMSectionFrameTests() => SetRendererInfo(new RendererInfo("Static", false));

    [Fact]
    public void TheChipsAreLinksAndPastSeasonsIsTheBridgeButton()
    {
        var board = Guid.NewGuid();
        var cut = RenderComponent<MoMSectionFrame>(p => p
            .Add(f => f.Active, "season")
            .Add(f => f.Mix, MixEnum.Phoenix)
            .Add(f => f.RecordBoardId, board));

        var links = cut.FindAll("nav.mom-frame-nav a");
        Assert.Contains(links, a => a.GetAttribute("href") == "/MarchOfMurlocs" && a.TextContent.Contains("This season"));
        Assert.Contains(links, a => a.GetAttribute("href") == "/TournamentBuilder" && a.TextContent.Contains("Planner"));
        Assert.Contains(links, a => a.GetAttribute("href") == $"/Tournament/Stamina/{board}/Record" && a.TextContent.Contains("Record a session"));
        var past = cut.Find("button[data-mom-seasons]");
        Assert.Contains("Past seasons", past.TextContent);
        Assert.Contains("mud-button-outlined", past.ClassList);
        // The active chip is filled; the others outlined.
        Assert.Contains("mud-button-filled", cut.Find("nav a[href='/MarchOfMurlocs']").ClassList);
        Assert.Contains("mud-button-outlined", cut.Find("nav a[href='/TournamentBuilder']").ClassList);
    }

    [Fact]
    public void WithoutABoardThereIsNoRecordChip()
    {
        var cut = RenderComponent<MoMSectionFrame>(p => p
            .Add(f => f.Active, "season")
            .Add(f => f.Mix, MixEnum.Phoenix2));

        Assert.DoesNotContain(cut.FindAll("nav.mom-frame-nav a"), a => a.TextContent.Contains("Record a session"));
        Assert.NotEmpty(cut.FindAll("button[data-mom-seasons]"));
    }
}
