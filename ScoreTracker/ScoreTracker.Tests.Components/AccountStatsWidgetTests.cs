using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Account Stats widget (the renamed Pumbility widget, TypeId still "pumbility"). Two
///     things worth pinning at this level: the glowy total + pools + competitive level
///     render, and the closest-matches list only admits public players or your non-region
///     community mates — with the community ones carrying the green glow.
/// </summary>
public sealed class AccountStatsWidgetTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _publicRival = Guid.NewGuid();
    private readonly Guid _crewMate = Guid.NewGuid();
    private readonly Guid _secretOutsider = Guid.NewGuid();

    public AccountStatsWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(_me, 5000, 26, 100, 0, 0,
                SkillRating: 868, SkillScore: 900000, SkillLevel: 21.5,
                SinglesRating: 852, SinglesScore: 900000, SinglesLevel: 21.3,
                DoublesRating: 774, DoublesScore: 880000, DoublesLevel: 19.9,
                CompetitiveLevel: 20.61, SinglesCompetitiveLevel: 21.34, DoublesCompetitiveLevel: 19.87));
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_users.Object);
        Services.AddScoped<CommunityGlowReader>();
        // The widget nests UserLabel/ScoreBreakdown, which gate their MudTooltip on
        // RendererInfo; declare the render world so bUnit can supply it.
        this.RenderInteractive();
    }

    private IRenderedComponent<PumbilityWidget> Render(string size, string configJson = "{}",
        MixEnum mix = MixEnum.Phoenix)
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "pumbility", null, 0, size, configJson, 1);
        return base.Render(builder =>
        {
            builder.OpenComponent<PumbilityWidget>(0);
            builder.AddAttribute(1, nameof(PumbilityWidget.Widget), widget);
            builder.AddAttribute(2, nameof(PumbilityWidget.EffectiveMix), mix);
            builder.CloseComponent();
        }).FindComponent<PumbilityWidget>();
    }

    private void SetUpRoster()
    {
        // The reader already applied visibility and sorted nearest first; the widget's job is to
        // say why each row is a peer and to keep board-only rivals at the end.
        _mediator.Setup(m => m.Send(It.IsAny<GetMyPeerRosterQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerList(new[]
                {
                    new PeerListEntry(new User(_crewMate, "CrewMate", false, null, new Uri("https://piu.test/c.png"), null),
                        21.33, false, new[] { "NorCal Pump" }, true, false),
                    new PeerListEntry(new User(_publicRival, "PublicRival", true, null, new Uri("https://piu.test/p.png"), null),
                        21.36, true, Array.Empty<string>(), true, true)
                },
                new[]
                {
                    new RivalSubject(Guid.NewGuid(), null, "PUMPKING#1", "PUMPKING#1", null, true,
                        RivalCapabilities.OfficialStandings, DateTimeOffset.MinValue)
                },
                Total: 41, MyLevel: 21.34));
    }

    [Fact]
    public void OneByOneRendersTheGlowyTotalPoolsAndCompetitiveLevel()
    {
        var cut = Render("1x1");

        Assert.Contains("rarity-glow-1", cut.Markup); // the glow the total keeps
        Assert.Contains("868", cut.Markup);           // total Pumbility
        Assert.Contains("852", cut.Markup);           // singles pool
        Assert.Contains("774", cut.Markup);           // doubles pool
        Assert.Contains("21.34", cut.Markup);         // singles competitive level
        Assert.Contains("19.87", cut.Markup);         // doubles competitive level
        // 1x1 is stats only — no match list.
        Assert.Empty(cut.FindAll(".dash-acct-matches"));
        // The weekly delta left for the Sessions page (owner, 2026-08-14), and the
        // history query that existed only to compute it left with it.
        Assert.Empty(cut.FindAll(".dash-stat-delta"));
        _mediator.Verify(m => m.Send(It.IsAny<GetPlayerHistoryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Phoenix2WearsTheLevelGemBesideTheTotal()
    {
        // 868 total sits below the 10,000 BRONZE floor → the UNRANKED rung, index 0.
        var cut = Render("1x1", mix: MixEnum.Phoenix2);

        var badge = Assert.Single(cut.FindAll(".pumbility-level-badge"));
        Assert.Contains("pumbility/p2/pumbility_00.png", badge.GetAttribute("src"));
    }

    [Fact]
    public void PhoenixHasNoLadderAndWearsNoGem()
    {
        var cut = Render("1x1");

        Assert.Empty(cut.FindAll(".pumbility-level-badge"));
    }

    [Fact]
    public void OneByTwoListsYourPeersWithWhyTheyArePeersAndBoardOnlyRivalsLast()
    {
        SetUpRoster();

        var cut = Render("1x2");

        Assert.NotEmpty(cut.FindAll(".dash-acct-matches"));
        Assert.Contains("Your peers · 41", cut.Markup);
        var rows = cut.FindAll("[data-testid='acct-peer']");
        Assert.Equal(2, rows.Count);
        // The clubmate wears the green edge and the club's initials; the rival wears the red edge
        // and every reason it is a peer.
        Assert.Contains("dash-lb-community", rows[0].ClassName);
        Assert.Contains("NP", rows[0].TextContent);
        Assert.Contains("is-rival", rows[1].ClassName);
        Assert.Contains("RIVAL", rows[1].TextContent);
        Assert.Contains("PMB", rows[1].TextContent);
        // The board-only rival closes the list with no level.
        var ghost = Assert.Single(cut.FindAll("[data-testid='acct-peer-ghost']"));
        Assert.Contains("PUMPKING#1", ghost.TextContent);
        Assert.Contains("BOARD", ghost.TextContent);
    }

    [Fact]
    public void TheConfiguredDimensionIsWhatTheRosterIsSortedOn()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyPeerRosterQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PeerList(Array.Empty<PeerListEntry>(), Array.Empty<RivalSubject>(), 0, 20.61));

        Render("1x2", "{\"matchDimension\":null}");

        // Combined dimension → null ChartType, the closest 25 as before.
        _mediator.Verify(m => m.Send(
            It.Is<GetMyPeerRosterQuery>(q => q.Dimension == null && q.Take == 25),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
