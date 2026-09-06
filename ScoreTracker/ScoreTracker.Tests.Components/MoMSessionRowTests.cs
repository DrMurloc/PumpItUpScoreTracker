using System;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     One ranked session on a March of Murlocs board (docs/design/march-of-murlocs.md §11.2):
///     the Official Leaderboards row skin, the player with avatar, the stored figures, the whole
///     row a link into the session, and the relationship said in words as well as colour.
/// </summary>
public sealed class MoMSessionRowTests : ComponentTestBase
{
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly User Kim = new(Guid.NewGuid(), Name.From("김재현"), true, null,
        new Uri("https://example.invalid/kim.png"), null);

    private static MoMBoardRow Row(int place = 1, int sessionNumber = 1, Uri? video = null)
    {
        return new MoMBoardRow(place, Session, Kim.Id, Kim, sessionNumber, 59319, 39, 24.2210,
            TimeSpan.FromSeconds(1324), new DateTimeOffset(2025, 2, 14, 0, 0, 0, TimeSpan.Zero), video);
    }

    public MoMSessionRowTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        SetRendererInfo(new RendererInfo("Static", false));
    }

    [Fact]
    public void TheRowIsOneLinkIntoTheSessionWearingTheBoardSkin()
    {
        var cut = RenderComponent<MoMSessionRow>(p => p.Add(r => r.Row, Row()));

        var link = cut.Find("a.olb-rank-card.mom-row");
        Assert.Equal($"/MarchOfMurlocs/Session/{Session}", link.GetAttribute("href"));
        Assert.Equal("1st", cut.Find(".mom-place").TextContent);
        Assert.Contains("p1", cut.Find(".mom-place").ClassList);
        Assert.Contains("김재현", cut.Find(".user-label").TextContent);
        Assert.Single(cut.FindAll("img.user-label-avatar"));
        Assert.Equal("59,319", cut.Find(".mom-total").TextContent);
        Assert.Contains("24.22", cut.Markup);
        Assert.Contains("22:04", cut.Markup);
        Assert.Contains("14 Feb", cut.Markup);
        Assert.Empty(cut.FindAll(".mom-runchip"));
        Assert.Contains("none", cut.Find(".mom-vid").ClassList);
    }

    [Fact]
    public void ASecondSessionOfOnePlayerSaysSoAndAVideoLightsTheIcon()
    {
        var cut = RenderComponent<MoMSessionRow>(p => p
            .Add(r => r.Row, Row(place: 4, sessionNumber: 2, video: new Uri("https://youtu.be/x"))));

        Assert.Equal("2nd session", cut.Find(".mom-runchip").TextContent);
        Assert.Equal("4th", cut.Find(".mom-place").TextContent);
        Assert.DoesNotContain("none", cut.Find(".mom-vid").ClassList);
    }

    [Fact]
    public void TheRelationshipRidesTheClassAndTheWords()
    {
        var cut = RenderComponent<MoMSessionRow>(p => p
            .Add(r => r.Row, Row(place: 2))
            .Add(r => r.RelationshipClass, "is-rival")
            .Add(r => r.RelationshipNote, "your rival"));

        var link = cut.Find("a.olb-rank-card");
        Assert.Contains("is-rival", link.ClassList);
        Assert.Contains("your rival", link.GetAttribute("title"));
        Assert.Equal("2nd, 김재현 — your rival", link.GetAttribute("aria-label"));
    }

    [Fact]
    public void ThePlaceSuffixFollowsEnglishOrdinals()
    {
        Assert.Equal("11th", RenderComponent<MoMSessionRow>(p => p.Add(r => r.Row, Row(place: 11))).Find(".mom-place").TextContent);
        Assert.Equal("23rd", RenderComponent<MoMSessionRow>(p => p.Add(r => r.Row, Row(place: 23))).Find(".mom-place").TextContent);
        Assert.Equal("12th", RenderComponent<MoMSessionRow>(p => p.Add(r => r.Row, Row(place: 12))).Find(".mom-place").TextContent);
    }
}
