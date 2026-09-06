using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
using Microsoft.Extensions.DependencyInjection;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.MoM;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Past-seasons dialog (docs/design/march-of-murlocs.md §11.8, D35): every season newest
///     first, one line per board with the count, the winner and your own result in words; each
///     row one link into that season's page. It loads on first open, never before.
/// </summary>
public sealed class MoMPastSeasonsIslandTests : ComponentTestBase
{
    private static readonly User Kim = new(Guid.NewGuid(), Name.From("김재현"), true, null,
        new Uri("https://example.invalid/kim.png"), null);
    private static readonly Guid Viewer = Guid.NewGuid();
    private static readonly DateTimeOffset Feb = new(2025, 2, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Winter = Guid.NewGuid();

    private static IReadOnlyList<MoMSeasonListing> Listing()
    {
        return new[]
        {
            new MoMSeasonListing(new MoMSeasonSummary(Guid.NewGuid(), "Summer 2026", Feb.AddMonths(18), Feb.AddMonths(20), true),
                new[]
                {
                    new MoMSeasonBoardListing(Guid.NewGuid(), ChartType.Double, 0, null, null, null, null),
                    new MoMSeasonBoardListing(Guid.NewGuid(), ChartType.Single, 0, null, null, null, null)
                }),
            new MoMSeasonListing(new MoMSeasonSummary(Winter, "Winter 2025", Feb, new DateTimeOffset(2025, 3, 31, 0, 0, 0, TimeSpan.Zero), false),
                new[]
                {
                    new MoMSeasonBoardListing(Guid.NewGuid(), ChartType.Double, 11, Kim, 59319, null, null),
                    new MoMSeasonBoardListing(Guid.NewGuid(), ChartType.Single, 5, Kim, 54431, 2, 42596)
                }),
            new MoMSeasonListing(new MoMSeasonSummary(Guid.NewGuid(), "Practice", Feb.AddMonths(-16), Feb.AddMonths(-15), false),
                new[] { new MoMSeasonBoardListing(Guid.NewGuid(), ChartType.Double, 2, Kim, 24880, 1, 24880) })
        };
    }

    public MoMPastSeasonsIslandTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Viewer, Name.From("DrMurloc"), true, null,
            new Uri("https://example.invalid/d.png"), null));
        Mediator.Setup(m => m.Send(It.IsAny<GetMoMSeasonsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Listing());
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedFragment RenderIsland()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MoMPastSeasonsIsland>(1);
            builder.AddAttribute(2, nameof(MoMPastSeasonsIsland.Mix), MixEnum.Phoenix);
            builder.CloseComponent();
        });
    }

    [Fact]
    public async Task OpeningListsEverySeasonWithWinnersAndYourResultInWords()
    {
        var cut = RenderIsland();
        Mediator.Verify(m => m.Send(It.IsAny<GetMoMSeasonsQuery>(), It.IsAny<CancellationToken>()), Times.Never);

        var island = cut.FindComponent<MoMPastSeasonsIsland>();
        await island.InvokeAsync(() => island.Instance.Open());

        Mediator.Verify(m => m.Send(It.Is<GetMoMSeasonsQuery>(q => q.Mix == MixEnum.Phoenix && q.ViewerId == Viewer),
            It.IsAny<CancellationToken>()), Times.Once);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid=mom-season-row]").Count));
        var rows = cut.FindAll("[data-testid=mom-season-row]");
        Assert.Contains("Summer 2026", rows[0].TextContent);
        Assert.Contains("running now", rows[0].TextContent);
        Assert.Contains("you have not played it", rows[0].TextContent);
        Assert.Equal($"/MarchOfMurlocs/{Winter}", rows[1].GetAttribute("href"));
        Assert.Contains("11 sessions", rows[1].TextContent);
        Assert.Contains("59,319", rows[1].TextContent);
        Assert.Contains("you sat this one out", rows[1].TextContent);
        Assert.Contains("you were 2nd — 42,596", rows[1].TextContent);
        Assert.Contains("you won it", rows[2].TextContent);
        Assert.Equal(2, rows[1].QuerySelectorAll("img.user-label-avatar").Length);
        Assert.DoesNotContain("won by", cut.Markup);
    }

    [Fact]
    public async Task AVisitorSeesNoResultLine()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        var cut = RenderIsland();
        var island = cut.FindComponent<MoMPastSeasonsIsland>();

        await island.InvokeAsync(() => island.Instance.Open());

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("[data-testid=mom-season-row]").Count));
        Assert.Empty(cut.FindAll(".mom-board-me"));
        Mediator.Verify(m => m.Send(It.Is<GetMoMSeasonsQuery>(q => q.ViewerId == null), It.IsAny<CancellationToken>()), Times.Once);
    }
}
