using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Quick Record widget (§4.2). Picking is exercised by invoking ChartSelector's
///     ChartIdSelected delegate directly — driving the live MudAutocomplete popup in
///     bUnit buys nothing the widget owns. The record row is what we assert on.
/// </summary>
public sealed class QuickRecordWidgetTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Chart _chart = MakeChart();

    public QuickRecordWidgetTests()
    {
        // DifficultyBubble's scoring-level cache resolves through the last-registered mediator.
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _chart });
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordedPhoenixScore?)null);
        // UpdatePhoenixBestAttemptCommand : IRequest (no response) → the non-generic
        // Task Send(IRequest, …) overload, so Returns(Task.CompletedTask), not ReturnsAsync.
        _mediator.Setup(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Services.AddSingleton(_mediator.Object);
        Services.AddScoped<ChartCatalogCache>();
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
    }

    private static Chart MakeChart() =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song("Bad Apple!! feat. Nomico", SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Nomico", Bpm.From(140, 140)),
            ChartType.Double, 22, MixEnum.Phoenix, null, 1200, new HashSet<Skill>());

    private static Chart MakeXxChart() =>
        new(Guid.NewGuid(), MixEnum.XX,
            new Song("Chimera", SongType.Arcade, new Uri("https://piu.test/xx.png"),
                TimeSpan.FromMinutes(2), "SHK", Bpm.From(200, 200)),
            ChartType.Single, 18, MixEnum.XX, null, 800, new HashSet<Skill>());

    private IRenderedComponent<QuickRecordWidget> Render(bool editMode = false, string configJson = "{}")
    {
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "quick-record", null, 0, "1x1", configJson, 1);
        return base.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<QuickRecordWidget>(1);
            builder.AddAttribute(2, nameof(QuickRecordWidget.Widget), widget);
            builder.AddAttribute(3, nameof(QuickRecordWidget.EffectiveMix), MixEnum.Phoenix);
            builder.AddAttribute(4, nameof(QuickRecordWidget.EditMode), editMode);
            builder.CloseComponent();
        }).FindComponent<QuickRecordWidget>();
    }

    private static Task Pick(IRenderedComponent<QuickRecordWidget> cut, Chart chart)
    {
        var selector = cut.FindComponent<ChartSelector>();
        return cut.InvokeAsync(() => selector.Instance.ChartIdSelected(chart));
    }

    [Fact]
    public void LoggedOutShowsTheSignInPrompt()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var cut = Render();

        Assert.Contains("Sign in to record.", cut.Markup);
    }

    [Fact]
    public void SearchStateShowsTheSelectorAndTheTargetMix()
    {
        var cut = Render();

        Assert.NotEmpty(cut.FindComponents<ChartSelector>());
        // "Posts to {0}" formatted with the resolved mix name.
        Assert.Contains("Posts to Phoenix", cut.Markup);
    }

    [Fact]
    public async Task PickingAChartWithABestPrefillsTheScore()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordedPhoenixScore(_chart.Id, 981200, PhoenixPlate.MarvelousGame, false,
                new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero)));
        var cut = Render();

        await Pick(cut, _chart);

        // The record row is up with the existing best filled in.
        Assert.NotEmpty(cut.FindAll(".qr-save-btn"));
        Assert.Contains("981200", cut.Markup);
    }

    [Fact]
    public async Task SavingDispatchesTheUpdateCommandWithTheCurrentEntryOnce()
    {
        // Drive the entry through the prefill path (a chart with an existing best) rather
        // than the MudNumericField UI — the assertion is that Save posts the current score,
        // plate, and broken flag as one command.
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecordedPhoenixScore(_chart.Id, 985320, PhoenixPlate.MarvelousGame, false,
                new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero)));
        var cut = Render();
        await Pick(cut, _chart);

        cut.Find(".qr-save-btn").Click();

        _mediator.Verify(m => m.Send(
            It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                c.ChartId == _chart.Id && c.Mix == MixEnum.Phoenix && !c.IsBroken
                && c.Plate == PhoenixPlate.MarvelousGame
                && c.Score != null && (int)c.Score.Value == 985320),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveIsDisabledUntilAScoreIsEntered()
    {
        var cut = Render();
        await Pick(cut, _chart);   // no existing best → score is null

        Assert.True(cut.Find(".qr-save-btn").HasAttribute("disabled"));
    }

    [Fact]
    public void EditModeDisablesTheSelector()
    {
        var cut = Render(editMode: true);

        Assert.True(cut.FindComponent<ChartSelector>().Instance.Disabled);
    }

    [Fact]
    public void AllMixesModeDisablesTheChartSelectorUntilAMixIsChosen()
    {
        var cut = Render(configJson: "{\"allMixes\":true}");

        Assert.NotEmpty(cut.FindComponents<MudSelect<MixEnum?>>());
        Assert.True(cut.FindComponent<ChartSelector>().Instance.Disabled);
        Assert.Contains("Select a mix", cut.Markup);
    }

    [Fact]
    public async Task PickingALegacyMixThenSavingUsesTheXxCommand()
    {
        var xxChart = MakeXxChart();
        _mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.XX), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { xxChart });
        _mediator.Setup(m => m.Send(It.IsAny<GetXXBestChartAttemptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BestXXChartAttempt(xxChart,
                new XXChartAttempt(XXLetterGrade.SSS, false, (XXScore?)98000,
                    new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero))));
        _mediator.Setup(m => m.Send(It.IsAny<UpdateXXBestAttemptCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cut = Render(configJson: "{\"allMixes\":true}");
        await cut.InvokeAsync(() =>
            cut.FindComponent<MudSelect<MixEnum?>>().Instance.ValueChanged.InvokeAsync(MixEnum.XX));
        await Pick(cut, xxChart); // legacy prefill sets the letter grade → Save enabled

        cut.Find(".qr-save-btn").Click();

        _mediator.Verify(m => m.Send(
            It.Is<UpdateXXBestAttemptCommand>(c =>
                c.chartId == xxChart.Id && c.Mix == MixEnum.XX && c.LetterGrade == XXLetterGrade.SSS && !c.IsBroken),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
