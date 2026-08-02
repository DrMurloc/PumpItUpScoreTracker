using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Renders the dialog for the same reason the hero has a render test: Razor accepts an
///     invented parameter at compile time and throws on first render, so a component nothing
///     ever renders is a component nothing ever checks.
/// </summary>
public sealed class ChartLeaderboardDialogTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();

    public ChartLeaderboardDialogTests()
    {
        var chart = TestChart();
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixScoresForChartQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetCompetitivePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        // DifficultyBubble reads scoring levels through this; an unstubbed mock hands back null
        // and the bubble dereferences it before the dialog's own markup ever renders.
        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<Guid, double>)new Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        ChartId = chart.Id;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private Guid ChartId { get; }

    [Fact]
    public void AnEmptyBoardNamesWhatWouldFillItRatherThanRenderingNothing()
    {
        var dialog = RenderDialog();

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-empty']")));
        // Every scope stays reachable — an unavailable one greys rather than disappearing.
        Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-World']"));
        Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-CompetitivePeers']"));
    }

    [Fact]
    public void TheCommunityPickerStaysHiddenWithoutTwoCommunities()
    {
        // A control with one choice is furniture, not a control (D19). Signed out here, so
        // there are none at all — the strictest version of the same rule.
        var dialog = RenderDialog();

        // Assert the dialog actually rendered before asserting on an absence, or the test
        // passes on a blank tree.
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-World']")));
        Assert.Empty(dialog.FindAll("[data-testid='cld-community-picker']"));
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment RenderDialog()
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartLeaderboardDialog>(1);
            builder.AddAttribute(2, nameof(ChartLeaderboardDialog.Visible), true);
            builder.AddAttribute(3, nameof(ChartLeaderboardDialog.ChartId), ChartId);
            builder.AddAttribute(4, nameof(ChartLeaderboardDialog.Mix), MixEnum.Phoenix);
            builder.CloseComponent();
        });
    }

    private static Chart TestChart()
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ScoreTracker.SharedKernel.Enums.ChartType.Single, DifficultyLevel.From(21),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }
}
