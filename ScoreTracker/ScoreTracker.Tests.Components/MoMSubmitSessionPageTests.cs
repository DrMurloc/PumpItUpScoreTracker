using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.MarchOfMurlocs;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Submit (march-of-murlocs.md §11.4): a draft renders its entries newest-first with the
///     frozen §1 zero-point treatment — an ordinary entry that says it scores zero, never a
///     non-play — publishing confirms and dispatches, and the budget legend narrates the
///     window including the legal overhang (§2.9).
/// </summary>
public sealed class MoMSubmitSessionPageTests : ComponentTestBase
{
    private static readonly Guid DraftId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly MoMSeasonRef Season = new(Guid.NewGuid(), "Summer 2026", 2026, 3);

    private readonly Chart _long;
    private readonly Chart _short;

    public MoMSubmitSessionPageTests()
    {
        _long = BuildChart("Gargoyle - FULL SONG -", 25, TimeSpan.FromSeconds(6300));
        _short = BuildChart("Slam", 24, TimeSpan.FromSeconds(99));

        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(d =>
            d.Now == new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)));
        Services.AddScoped<ChartCatalogCache>();

        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(Me, Name.From("Me"), true, null,
            new Uri("https://example.invalid/a.png"), null));
        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _long, _short });

        Renderer.SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void DraftEntriesRenderNewestFirstWithTheFrozenZeroPointTreatment()
    {
        // Slam at 740k is in the zeroed band: still an ordinary entry (§1) — counted, amber,
        // and the tooltip says it counted rather than promising a free retry.
        WithDraft(
            ChartRow(0, _short, 976489, 1528),
            ChartRow(1, _long, 740000, 0));

        var page = Render();

        var rows = page.FindAll(".mom-erow");
        Assert.Equal(2, rows.Count);
        // Newest first: the last-entered chart leads, its ordinal still the real play order.
        Assert.Contains("Gargoyle", rows[0].TextContent);
        Assert.Contains("2", rows[0].QuerySelector(".ord")!.TextContent);
        Assert.Contains("scores zero", rows[0].TextContent);
        Assert.Contains("still counts as one of your charts", rows[0].GetAttribute("title"));
        Assert.Contains("2 charts", page.Find(".mom-total").TextContent);
    }

    [Fact]
    public void TheBudgetLegendNarratesALegalOverhang()
    {
        // One 105-minute song fills the window exactly at its start: everything before the
        // last chart is zero, so the draft is legal, and nothing more can start.
        WithDraft(ChartRow(0, _long, 950000, 3000));

        var page = Render();

        Assert.Contains("the window is exactly full", page.Find(".mom-budget-legend").TextContent);
    }

    [Fact]
    public async Task PublishConfirmsThenDispatchesAndLandsOnTheBreakdown()
    {
        WithDraft(ChartRow(0, _short, 976489, 1528));
        Mediator.Setup(m => m.Send(It.IsAny<PublishMoMSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var page = Render();

        await page.FindAll("button").First(b => b.TextContent.Trim() == "Publish")
            .ClickAsync(new MouseEventArgs());
        Assert.Contains("Publish this session?", page.Markup);
        // The provider renders before the page, so the dialog's confirm is the FIRST Publish.
        await page.FindAll("button").First(b => b.TextContent.Trim() == "Publish")
            .ClickAsync(new MouseEventArgs());

        Mediator.Verify(m => m.Send(It.Is<PublishMoMSessionCommand>(c => c.SessionId == DraftId),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.EndsWith($"/MarchOfMurlocs/Session/{DraftId}",
            Services.GetRequiredService<NavigationManager>().Uri);
    }

    private void WithDraft(params MoMSessionChartRow[] rows)
    {
        var view = new MoMSessionView(DraftId, BoardId, Season, MixEnum.Phoenix,
            ChartType.Double, Me, "Me", null, rows.Sum(r => r.SessionScore), rows.Length,
            TimeSpan.Zero, 24, 10, 23, 25, null, null, TimeSpan.FromMinutes(105), false, rows);
        Mediator.Setup(m => m.Send(It.Is<GetMoMSessionQuery>(q => q.SessionId == DraftId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
        Mediator.Setup(m => m.Send(It.IsAny<SaveMoMSessionDraftCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(view);
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment Render()
    {
        return base.Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<SubmitSession>(1);
            builder.AddAttribute(2, nameof(SubmitSession.IdText), DraftId.ToString());
            builder.CloseComponent();
        });
    }

    private static MoMSessionChartRow ChartRow(int ordinal, Chart chart, int score, int points)
    {
        return new MoMSessionChartRow(ordinal, chart.Id, score, PhoenixPlate.RoughGame, false,
            points, 0, null, (int)chart.Level + 0.5);
    }

    private static Chart BuildChart(string name, int level, TimeSpan duration)
    {
        var song = new Song(name, SongType.Arcade, new Uri("https://example.invalid/a.png"),
            duration, "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Double,
            DifficultyLevel.From(level), MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }
}
