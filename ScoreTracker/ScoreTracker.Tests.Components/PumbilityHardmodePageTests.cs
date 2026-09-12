using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Web.Pages.Progress;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Hardmode tab's load path. It had none, which is how a change that left the page on its
///     loading card forever went out under a green suite: the page prices its OWN record, unlike
///     every other tab in the section, and nothing rendered it.
/// </summary>
public sealed class PumbilityHardmodePageTests : ComponentTestBase
{
    private static readonly Guid Me = Guid.NewGuid();

    [Fact]
    public async Task ThePagePricesItsRecordOnceTheFrameHasResolved()
    {
        var cut = Render(pool: null);
        await WaitForLoad(cut);

        // The patience card is what shows while _page is null. If it is still here, the page
        // never priced anything.
        Assert.DoesNotContain("pmb-frame-waiting", cut.Markup);
        Assert.Contains("pmb-poolsplit", cut.Markup);
    }

    [Fact]
    public async Task ThePageAsksForTheFramesSavedPoolNotTheCombinedOne()
    {
        // The frame resolves Doubles from settings after its own first render. A page that
        // seeded once on ITS first render asked for the combined fifty and labelled it Doubles.
        var cut = Render(pool: ChartType.Double);
        await WaitForLoad(cut);

        Mediator.Verify(m => m.Send(
            It.Is<GetHardmodePageQuery>(q => q.Pool == ChartType.Double && q.UserId == Me),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Mediator.Verify(m => m.Send(
            It.Is<GetHardmodePageQuery>(q => q.Pool == null),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task WaitForLoad(IRenderedFragment cut)
    {
        // The record is queued from the body's render, so it lands a pass or two later.
        cut.WaitForState(() => !cut.Markup.Contains("pmb-frame-waiting"), TimeSpan.FromSeconds(5));
        await Task.CompletedTask;
    }

    private IRenderedComponent<PumbilityHardmode> Render(ChartType? pool)
    {
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(u => u.User).Returns(new User(Me, "Probe", true, null, new Uri("https://piu.test/me.png"), null));
        UiSettings.Setup(s => s.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix2);
        UiSettings.Setup(s => s.GetSetting(PumbilitySectionFrame.PoolSettingKey))
            .ReturnsAsync(pool?.ToString() ?? string.Empty);
        UiSettings.Setup(s => s.GetSetting(It.Is<string>(k => k != PumbilitySectionFrame.PoolSettingKey)))
            .ReturnsAsync(string.Empty);

        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityPageRecord(MixEnum.Phoenix2, null, 0, null, null,
                Array.Empty<PoolEntry>(), Array.Empty<PoolEntry>(), Array.Empty<PumbilityTarget>()));
        Mediator.Setup(m => m.Send(It.IsAny<GetHardmodePageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetHardmodePageQuery q, CancellationToken _) => HardmodePage(q.Pool));

        // The board section resolves the shared glow reader, which is a real service rather
        // than a port - registering it is cheaper than stubbing what it would have told us.
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
        Mediator.Setup(m => m.Send(It.IsAny<GetHardmodeBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HardmodeBoardRow>());
        Mediator.Setup(m => m.Send(It.IsAny<GetOfficialHardmodeBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OfficialHardmodeRow>());
        // Everything the page's children ask for. None of them is what this test is about - the
        // page's own load path is - but an unstubbed Moq Send returns null and takes the render
        // down before the assertion.
        Mediator.Setup(m => m.Send(It.IsAny<GetPlayersStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerStatsRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetHardmodeChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HardmodeChartRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, ChartIdentityRecord>)new Dictionary<Guid, ChartIdentityRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetPeerStandingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PeerStanding>());

        var users = new Mock<IUserReader>();
        users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(Array.Empty<User>());
        Services.AddSingleton(users.Object);
        Services.AddScoped<CommunityGlowReader>();

        this.RenderInteractive();
        return RenderComponent<PumbilityHardmode>();
    }

    private static HardmodePageRecord HardmodePage(ChartType? pool) =>
        new(MixEnum.Phoenix2, pool,
            new HardmodePoolTotals(1200, 12, 4, 30), new HardmodePoolTotals(800, 8, 5, 20),
            new HardmodePoolTotals(400, 4, 6, 10), Array.Empty<PoolEntry>(), Array.Empty<PoolEntry>(),
            Array.Empty<TitleRail>(), 1208);
}
