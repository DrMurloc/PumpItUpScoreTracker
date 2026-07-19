using System;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The SRP's ✎ dialog hosts the shared RecordScoreForm with the chart pre-filled —
///     saving dispatches the ledger command and hands the family-shaped result back.
/// </summary>
public sealed class ChartQuickRecordDialogTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private RecordScoreForm.Result? _saved;

    public ChartQuickRecordDialogTests()
    {
        _mediator.Setup(m => m.Send(
                It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Collections.Generic.Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
        Services.AddScoped<ScoreTracker.Web.Services.ChartScoringLevels>();
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private IRenderedFragment RenderDialog()
    {
        var target = ChartsPageTests.MakeResult("District 1", 21);
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartQuickRecordDialog>(1);
            builder.AddAttribute(2, nameof(ChartQuickRecordDialog.Visible), true);
            builder.AddAttribute(3, nameof(ChartQuickRecordDialog.Target), target);
            builder.AddAttribute(4, nameof(ChartQuickRecordDialog.OnSaved),
                Microsoft.AspNetCore.Components.EventCallback.Factory.Create<RecordScoreForm.Result>(this,
                    r => _saved = r));
            builder.CloseComponent();
        });
    }

    [Fact]
    public void SavingAScoreDispatchesTheLedgerCommandAndReturnsTheResult()
    {
        var cut = RenderDialog();
        cut.WaitForAssertion(() => Assert.Contains("District 1", cut.Markup));

        var scoreInput = cut.Find(".qr-score-field input");
        scoreInput.Input("987654");
        scoreInput.Change("987654");
        cut.WaitForAssertion(() =>
            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save" && !b.HasAttribute("disabled")));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();

        cut.WaitForAssertion(() =>
        {
            _mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                c.Score != null && (int)c.Score.Value == 987654 && !c.IsBroken), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.NotNull(_saved);
            Assert.Equal(987654, (int)_saved!.PhoenixScore!.Value);
        });
    }
}
