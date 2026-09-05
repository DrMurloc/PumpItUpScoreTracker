using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;
using Chart = ScoreTracker.SharedKernel.Models.Chart;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The chart page's record panel reads and writes through the store the mix in view
///     actually uses. It used to be Phoenix-only: on a legacy mix it asked the Phoenix
///     store (so a real record read as "No score yet"), offered a 0-1,000,000 box with a
///     plate dropdown, and wrote the answer back into the Phoenix store.
/// </summary>
public sealed class ChartRecordPanelTests : ComponentTestBase
{
    private static readonly Guid ChartId = Guid.NewGuid();
    private readonly Mock<IMediator> _mediator = new();

    public ChartRecordPanelTests()
    {
        CurrentUser.SetupGet(u => u.IsLoggedIn).Returns(true);
        Services.AddSingleton(_mediator.Object);
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(c => c.Now == DateTimeOffset.UtcNow));

        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeChart(MixEnum.Prime2) }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoreJourneyQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreJournalEntry>().AsEnumerable());
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private static Chart MakeChart(MixEnum mix) => new(ChartId, mix,
        new Song("Bad Apple!! feat. Nomico", SongType.Arcade, new Uri("https://piu.test/art.png"),
            TimeSpan.FromSeconds(100), "Alstroemeria Records", Bpm.From(140, 140)),
        ChartType.Single, 5, mix, "ANDAMIRO", 400);

    private IRenderedComponent<ChartRecordPanel> RenderPanel(MixEnum mix) =>
        RenderComponent<ChartRecordPanel>(p => p
            .Add(c => c.ChartId, ChartId)
            .Add(c => c.Mix, mix));

    /// <summary>
    ///     A Prime 2 record lives in BestAttempt as a letter grade. Asking the Phoenix store
    ///     for it returns nothing, which is exactly how a player's real score came to read as
    ///     "No score yet" on the page that had just saved it.
    /// </summary>
    [Fact]
    public void ALegacyMixReadsItsBestFromTheLegacyStore()
    {
        _mediator.Setup(m => m.Send(It.Is<GetXXBestChartAttemptQuery>(q => q.Mix == MixEnum.Prime2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BestXXChartAttempt(MakeChart(MixEnum.Prime2),
                new XXChartAttempt(XXLetterGrade.S, false, (XXScore)255800, DateTimeOffset.UtcNow)));

        var cut = RenderPanel(MixEnum.Prime2);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("No score yet", cut.Markup);
            // The letter is drawn as art; the era number rides beside it, plainly.
            Assert.Contains("/letters/s.png", cut.Markup);
            Assert.Contains("255,800", cut.Markup);
        });
        _mediator.Verify(m => m.Send(It.IsAny<GetPhoenixRecordQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     And the panel never quotes a percentile on a legacy mix: score rankings are built
    ///     from Phoenix scores, and era scores are note-count dependent.
    /// </summary>
    [Fact]
    public void ALegacyMixAsksForNoScoreRanking()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetXXBestChartAttemptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BestXXChartAttempt(MakeChart(MixEnum.Prime2), null));

        var cut = RenderPanel(MixEnum.Prime2);

        cut.WaitForAssertion(() => Assert.Contains("No score yet", cut.Markup));
        _mediator.Verify(m => m.Send(It.IsAny<GetPeerStandingsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    ///     Recording on a legacy mix writes a letter grade to the legacy store. The old panel
    ///     sent UpdatePhoenixBestAttemptCommand whatever the mix, which put real rows in a
    ///     store no legacy read path consults.
    /// </summary>
    [Fact]
    public async Task RecordingOnALegacyMixWritesALetterGradeNotAPhoenixScore()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetXXBestChartAttemptQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BestXXChartAttempt(MakeChart(MixEnum.Prime2), null));

        var cut = RenderPanel(MixEnum.Prime2);
        cut.WaitForAssertion(() => Assert.Contains("No score yet", cut.Markup));

        await cut.Find(".chart-record-edit").ClickAsync(new MouseEventArgs());

        // The strip is SSS, SS, S, A: the grade a legacy player picks IS the score.
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll(".qr-grade-opt").Count));
        await cut.FindAll(".qr-grade-opt")[2].ClickAsync(new MouseEventArgs());
        await cut.Find(".qr-save-btn").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            _mediator.Verify(m => m.Send(It.Is<UpdateXXBestAttemptCommand>(c =>
                    c.Mix == MixEnum.Prime2 && c.LetterGrade == XXLetterGrade.S && !c.IsBroken),
                It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<UpdatePhoenixBestAttemptCommand>(),
                It.IsAny<CancellationToken>()), Times.Never);
        });
    }

    /// <summary>Phoenix keeps the score box and the Phoenix store — the branch cuts both ways.</summary>
    [Fact]
    public async Task RecordingOnPhoenixStillWritesAPhoenixScore()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeChart(MixEnum.Phoenix) }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordedPhoenixScore?)null);
        _mediator.Setup(m => m.Send(It.IsAny<GetPeerStandingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, PeerStanding>)new Dictionary<Guid, PeerStanding>());

        var cut = RenderPanel(MixEnum.Phoenix);
        cut.WaitForAssertion(() => Assert.Contains("No score yet", cut.Markup));

        await cut.Find(".chart-record-edit").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".qr-grade-opt")));

        var scoreInput = cut.Find(".qr-score-field input");
        await scoreInput.InputAsync(new ChangeEventArgs { Value = "987654" });
        await scoreInput.ChangeAsync(new ChangeEventArgs { Value = "987654" });
        cut.WaitForAssertion(() => Assert.False(cut.Find(".qr-save-btn").HasAttribute("disabled")));
        await cut.Find(".qr-save-btn").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            _mediator.Verify(m => m.Send(It.Is<UpdatePhoenixBestAttemptCommand>(c =>
                    c.Mix == MixEnum.Phoenix && c.Score != null && (int)c.Score.Value == 987654),
                It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<UpdateXXBestAttemptCommand>(),
                It.IsAny<CancellationToken>()), Times.Never);
        });
    }
}
