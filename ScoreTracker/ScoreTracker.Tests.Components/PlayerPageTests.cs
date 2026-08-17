using System;
using System.Collections.Generic;
using System.Threading;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Pages.Progress;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The player page: it goes home when the profile read says no, shows the official card only
///     for a linked account, compares only when a signed-in viewer is looking at someone else, and
///     wears the level gem only on Phoenix 2.
/// </summary>
public sealed class PlayerPageTests : ComponentTestBase
{
    private static readonly Guid TargetId = Guid.NewGuid();
    private readonly Mock<IUiSettingsAccessor> _settings = new();

    public PlayerPageTests()
    {
        _settings.Setup(u => u.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix2);
        Services.AddSingleton(_settings.Object);
        Services.AddScoped<ChartCatalogCache>();

        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(new PlayerVisibility(true, false, true, false, Array.Empty<Name>())));
        Mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerStandingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfficialPlayerStandingRecord?)null);
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(Guid.NewGuid(), 0, 1, 0, 0, 0, 867, 0, 1,
                0, 0, 1, 0, 0, 1, 20.6, 20.8, 20.2));
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerHeadToHeadQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RivalHeadToHeadRecord(new HeadToHeadSubject(TargetId, "Reno", null), 0, 0, 0,
                Array.Empty<RivalHeadToHeadRow>()));
        // The page's loading state is a PatienceCard, which draws a phrase through the RNG seam.
        Services.AddSingleton(new Mock<IRandomNumberGenerator>().Object);
    }

    private static PlayerProfileRecord Profile(PlayerVisibility visibility) => new(TargetId, Name.From("Reno"),
        new Uri("https://piu.test/avatar.png"), Name.From("United States"), visibility,
        17_412, 61_240, 30_118, 31_122, 21.63, 22.05, 24, 812,
        new[]
        {
            new PlayerFolderCompletionRecord(ChartType.Single, 20, 25, 50,
                new Dictionary<PhoenixLetterGrade, int> { [PhoenixLetterGrade.AA] = 25 }),
            new PlayerFolderCompletionRecord(ChartType.Double, 20, 24, 48,
                new Dictionary<PhoenixLetterGrade, int> { [PhoenixLetterGrade.A] = 24 })
        });

    private void GivenViewer(Guid id)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(id, Name.From("Viewer"), true, null,
            new Uri("https://piu.test/viewer.png"), null));
    }

    private IRenderedComponent<Player> Render()
    {
        this.RenderInteractive();
        return RenderComponent<Player>(p => p.Add(x => x.UserIdParam, TargetId));
    }

    [Fact]
    public void ADeniedOrMissingPlayerGoesHome()
    {
        GivenViewer(Guid.NewGuid());
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerProfileRecord?)null);

        var cut = Render();

        Assert.Contains(Services.GetRequiredService<FakeNavigationManager>().History, h => h.Uri == "/");
        Assert.Empty(cut.FindAll("[data-testid='player-hero']"));
    }

    [Fact]
    public void TheHeroShowsThePlayerAndBothCompetitiveLevelsNeverTheOverall()
    {
        GivenViewer(Guid.NewGuid());

        var cut = Render();

        Assert.Contains("Reno", cut.Markup);
        Assert.Contains("17,412", cut.Markup);
        Assert.Contains("21.63", cut.Markup);
        Assert.Contains("22.05", cut.Markup);
        Assert.Contains("Competitive Level", cut.Markup);
        Assert.DoesNotContain("Official Top", cut.Markup);
        // The Phoenix 2 gem sits beside the number.
        Assert.NotEmpty(cut.FindComponents<PumbilityLevelBadge>());
    }

    [Fact]
    public void ALinkedPlayerGetsTheOfficialCardWithTheBoardLink()
    {
        GivenViewer(Guid.NewGuid());
        Mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerStandingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialPlayerStandingRecord("RENO", 88, 61, 2, 4, 90, 85, null));

        var cut = Render();

        // Phoenix 2 boards run 300 deep; the chip says so.
        Assert.Contains("61 Official Top 300s", cut.Markup);
        Assert.Contains("/OfficialLeaderboards/Players?player=RENO", cut.Markup);
        Assert.Contains("#88", cut.Markup);
        Assert.Contains("#90", cut.Markup);
        Assert.Contains("#85", cut.Markup);
    }

    [Fact]
    public void TheHeadToHeadRendersForAnotherPlayerButNotYourself()
    {
        GivenViewer(Guid.NewGuid());
        Assert.NotEmpty(Render().FindAll("[data-testid='head-to-head']"));

        GivenViewer(TargetId);
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(new PlayerVisibility(true, true, true, false, Array.Empty<Name>())));
        Assert.Empty(Render().FindAll("[data-testid='head-to-head']"));
    }

    [Fact]
    public void TheTagsNameTheBasisYouSeeAPrivatePlayerOn()
    {
        GivenViewer(Guid.NewGuid());
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(new PlayerVisibility(true, false, false, true, new[] { Name.From("Seoul Pump") })));

        var cut = Render();

        Assert.Contains("Seoul Pump", cut.Markup);
        Assert.Contains("rvl-tag-rival", cut.Markup);
        // Private and not you: no Sessions link, the page's own gate for that family stays public-or-you.
        Assert.DoesNotContain($"/Player/{TargetId}/Sessions", cut.Markup);
    }

    [Fact]
    public void OnPhoenixThereIsNoGemAndOnXXThereAreNoPhoenixNumbers()
    {
        GivenViewer(Guid.NewGuid());
        _settings.Setup(u => u.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix);
        var phoenix = Render();
        Assert.Empty(phoenix.FindComponents<PumbilityLevelBadge>());
        Assert.Contains("17,412", phoenix.Markup);

        _settings.Setup(u => u.GetSelectedMix()).ReturnsAsync(MixEnum.XX);
        var xx = Render();
        Assert.DoesNotContain("PUMBILITY", xx.Markup);
        Assert.DoesNotContain("Total Rating", xx.Markup);
        Assert.Contains("Folder Completion", xx.Markup);
    }
}
