using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The app-bar search: three sections in one list, in a fixed order with fixed caps, each
///     label a row you cannot land on; a blank box asks the server nothing; picking a row navigates
///     to the right page for its kind.
/// </summary>
public sealed class SiteSearchTests : ComponentTestBase
{
    private static readonly Guid RoxyId = Guid.NewGuid();
    private static readonly Guid RobbyId = Guid.NewGuid();

    public SiteSearchTests()
    {
        // Twelve charts whose names contain "ro" — more than the section's cap of eight.
        var charts = Enumerable.Range(1, 12)
            .Select(i => ChartSlugsTests.BuildChart(song: $"Rock the house {i:00}", level: 10 + i,
                type: i % 2 == 0 ? ChartType.Double : ChartType.Single))
            .ToArray();
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        Mediator.Setup(m => m.Send(It.IsAny<SearchPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PlayerSearchHit(RoxyId, Name.From("Roxy"), Name.From("roxy#3"), new Uri("https://piu.test/roxy.png"),
                    null, new PlayerVisibility(true, false, false, false, new[] { Name.From("Seoul Pump") })),
                new PlayerSearchHit(RobbyId, Name.From("Robby"), null, new Uri("https://piu.test/robby.png"),
                    null, new PlayerVisibility(true, false, true, true, Array.Empty<Name>()))
            });
        Mediator.Setup(m => m.Send(It.IsAny<SearchOfficialBoardTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new OfficialPlayerRecord(7, "ROXY#4471", null, RoxyId),
                new OfficialPlayerRecord(8, "PROTO#1180", null, null)
            });
        // The linked board player is Roxy's account, seen on Roxy's bases.
        Mediator.Setup(m => m.Send(It.Is<GetPlayersVisibilityQuery>(q => q.UserIds.Contains(RoxyId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PlayerVisibility>
            {
                [RoxyId] = new(true, false, false, false, new[] { Name.From("Seoul Pump") })
            });
    }

    private IRenderedComponent<SiteSearch> Render()
    {
        this.RenderInteractive();
        return RenderComponent<SiteSearch>(p => p.Add(s => s.Mix, MixEnum.Phoenix2));
    }

    private static Task<IEnumerable<SiteSearch.SearchHit>> SearchFor(IRenderedComponent<SiteSearch> cut, string term) =>
        cut.FindComponent<MudAutocomplete<SiteSearch.SearchHit>>().Instance.SearchFunc!(term, CancellationToken.None);

    [Fact]
    public async Task ThreeSectionsInOrderEachCappedEachUnderALabelYouCannotPick()
    {
        var cut = Render();

        var hits = (await SearchFor(cut, "ro")).ToArray();

        var labels = hits.Where(h => h.IsHeader).Select(h => h.Label).ToArray();
        Assert.Equal(new[] { "Charts", "Players", "Board players" }, labels);
        Assert.Equal(8, hits.Count(h => h.Chart != null));
        Assert.Equal(2, hits.Count(h => h.Player != null));
        Assert.Equal(2, hits.Count(h => h.Board != null));
        // The label rows are disabled, which is what keeps the arrow keys off them.
        var disabled = cut.FindComponent<MudAutocomplete<SiteSearch.SearchHit>>().Instance.ItemDisabledFunc!;
        Assert.All(hits.Where(h => h.IsHeader), h => Assert.True(disabled(h)));
        Assert.All(hits.Where(h => !h.IsHeader), h => Assert.False(disabled(h)));
    }

    [Fact]
    public async Task ABlankBoxShowsChartsAndAsksTheServerNothing()
    {
        var cut = Render();

        var hits = (await SearchFor(cut, "")).ToArray();

        Assert.Equal(8, hits.Count(h => h.Chart != null));
        Assert.DoesNotContain(hits, h => h.Player != null || h.Board != null);
        Mediator.Verify(m => m.Send(It.IsAny<SearchPlayersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
        Mediator.Verify(m => m.Send(It.IsAny<SearchOfficialBoardTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     A board player whose tag is linked to an account glows on that account's bases — the
    ///     same green/red/both ladder a site player's row wears; an unlinked tag glows on nothing.
    /// </summary>
    [Fact]
    public async Task ALinkedBoardPlayerCarriesTheLinkedAccountsVisibilityAndAnUnlinkedOneNone()
    {
        var cut = Render();

        var hits = (await SearchFor(cut, "ro")).ToArray();

        var roxy = hits.Single(h => h.Board?.UserId == RoxyId);
        Assert.Equal(new[] { Name.From("Seoul Pump") }, roxy.Visibility!.SharedCommunities);
        Assert.Null(hits.Single(h => h.Board is { UserId: null }).Visibility);
    }

    [Fact]
    public async Task ASectionWithNoRowsHasNoLabel()
    {
        Mediator.Setup(m => m.Send(It.IsAny<SearchOfficialBoardTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OfficialPlayerRecord>());
        var cut = Render();

        var hits = (await SearchFor(cut, "ro")).ToArray();

        Assert.DoesNotContain(hits, h => h.IsHeader && h.Label == "Board players");
    }

    [Fact]
    public async Task PickingARowNavigatesToItsKindOfPage()
    {
        var cut = Render();
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        var autocomplete = cut.FindComponent<MudAutocomplete<SiteSearch.SearchHit>>().Instance;
        var hits = (await SearchFor(cut, "ro")).ToArray();

        await cut.InvokeAsync(() => autocomplete.ValueChanged.InvokeAsync(hits.First(h => h.Player?.UserId == RoxyId)));
        Assert.EndsWith($"/Player/{RoxyId}", nav.Uri);
        Assert.True(nav.History.Last().Options.ForceLoad);

        await cut.InvokeAsync(() => autocomplete.ValueChanged.InvokeAsync(hits.First(h => h.Board != null)));
        Assert.EndsWith("/OfficialLeaderboards/Players?player=ROXY%234471", nav.Uri);
        Assert.True(nav.History.Last().Options.ForceLoad);

        // Full loads, not enhanced navigation: a circuit's NavigateTo ignores the body's
        // data-enhance-nav="false", and an enhanced landing on a static SSR chart page patches
        // the DOM without re-running module scripts — the step chart arrives unmountable.
        await cut.InvokeAsync(() => autocomplete.ValueChanged.InvokeAsync(hits.First(h => h.Chart != null)));
        Assert.Contains("/Charts/", nav.Uri);
        Assert.True(nav.History.Last().Options.ForceLoad);

        // A label row is not a destination.
        var before = nav.Uri;
        await cut.InvokeAsync(() => autocomplete.ValueChanged.InvokeAsync(hits.First(h => h.IsHeader)));
        Assert.Equal(before, nav.Uri);
    }
}
