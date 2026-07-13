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
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerHistoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerRatingRecord>());
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(_users.Object);
        Services.AddScoped<CommunityGlowReader>();
    }

    private IRenderedComponent<PumbilityWidget> Render(string size, string configJson = "{}")
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "pumbility", null, 0, size, configJson, 1);
        return base.Render(builder =>
        {
            builder.OpenComponent<PumbilityWidget>(0);
            builder.AddAttribute(1, nameof(PumbilityWidget.Widget), widget);
            builder.AddAttribute(2, nameof(PumbilityWidget.EffectiveMix), MixEnum.Phoenix);
            builder.CloseComponent();
        }).FindComponent<PumbilityWidget>();
    }

    private void SetUpMatches()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCompetitiveNeighborsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CompetitiveNeighborRecord(_crewMate, 21.33),
                new CompetitiveNeighborRecord(_publicRival, 21.36),
                new CompetitiveNeighborRecord(_secretOutsider, 21.35)
            });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User(_publicRival, "PublicRival", true, null, new Uri("https://piu.test/p.png"), null),
                new User(_crewMate, "CrewMate", false, null, new Uri("https://piu.test/c.png"), null),
                new User(_secretOutsider, "SecretPlayer", false, null, new Uri("https://piu.test/s.png"), null)
            });
        // CommunityGlowReader keeps non-regional, non-"World" crews and their members.
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityOverviewRecord("Crew", CommunityPrivacyType.Public, 5, false) });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _crewMate });
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
    }

    [Fact]
    public void OneByTwoShowsPublicAndCommunityMatchesButNotPrivateOutsiders()
    {
        SetUpMatches();

        var cut = Render("1x2");

        Assert.NotEmpty(cut.FindAll(".dash-acct-matches"));
        Assert.Contains("PublicRival", cut.Markup);        // public → eligible
        Assert.Contains("CrewMate", cut.Markup);           // private but shares a community → eligible
        Assert.DoesNotContain("SecretPlayer", cut.Markup); // private, no shared community → hidden
        // The community mate carries the green glow; the public rival does not.
        Assert.Contains("dash-lb-community", cut.Markup);
    }

    [Fact]
    public void CombinedDimensionMatchesOnTheCombinedCompetitiveLevelWithinRangeOne()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetCompetitiveNeighborsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompetitiveNeighborRecord>());
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());

        Render("1x2", "{\"matchDimension\":null}");

        // Combined dimension → null ChartType, the combined competitive level, hard ±1.0 range.
        _mediator.Verify(m => m.Send(
            It.Is<GetCompetitiveNeighborsQuery>(q =>
                q.Dimension == null && q.MyLevel == 20.61 && q.Range == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
