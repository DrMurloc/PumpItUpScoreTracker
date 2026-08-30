using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartStageBreaksHandlerTests
{
    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly Guid Viewer = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private readonly Mock<IScoreJournalRepository> _journal = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private GetChartStageBreaksHandler Build()
    {
        return new GetChartStageBreaksHandler(_journal.Object, _cache);
    }

    private void SetupRows(params ChartStageBreakRow[] rows)
    {
        _journal.Setup(j => j.GetChartStageBreaks(MixEnum.Phoenix2, ChartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
    }

    [Fact]
    public async Task RowsComeBackAnonymizedWithTheViewersOwnFlagged()
    {
        SetupRows(
            new ChartStageBreakRow(Viewer, new JudgementCounts(700, 2, 0, 0, 1), true,
                "PerfectGame", "SSS+"),
            new ChartStageBreakRow(Stranger, new JudgementCounts(100, 0, 0, 5, 20), false));

        var rows = (await Build().Handle(new GetChartStageBreaksQuery(ChartId, MixEnum.Phoenix2, Viewer),
            CancellationToken.None)).ToArray();

        Assert.Equal(2, rows.Length);
        Assert.Equal(703, rows[0].Judged);
        Assert.True(rows[0].IsNonLifebarBreak);
        Assert.True(rows[0].IsViewer);
        Assert.Equal("PerfectGame", rows[0].PassPlate);
        Assert.Equal("SSS+", rows[0].PassGrade);
        Assert.Null(rows[1].PassPlate);
        Assert.Equal(125, rows[1].Judged);
        Assert.False(rows[1].IsViewer);
    }

    [Fact]
    public async Task TheAnonymousReadCachesWhileTheViewerFlagStaysPerRequest()
    {
        SetupRows(new ChartStageBreakRow(Viewer, new JudgementCounts(10, 0, 0, 0, 0), false));
        var handler = Build();

        var asStranger = (await handler.Handle(new GetChartStageBreaksQuery(ChartId, MixEnum.Phoenix2),
            CancellationToken.None)).Single();
        var asViewer = (await handler.Handle(new GetChartStageBreaksQuery(ChartId, MixEnum.Phoenix2, Viewer),
            CancellationToken.None)).Single();

        Assert.False(asStranger.IsViewer);
        Assert.True(asViewer.IsViewer);
        _journal.Verify(j => j.GetChartStageBreaks(MixEnum.Phoenix2, ChartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AChartWithNoBreaksAnswersEmpty()
    {
        SetupRows();

        Assert.Empty(await Build().Handle(new GetChartStageBreaksQuery(ChartId, MixEnum.Phoenix2),
            CancellationToken.None));
    }
}
