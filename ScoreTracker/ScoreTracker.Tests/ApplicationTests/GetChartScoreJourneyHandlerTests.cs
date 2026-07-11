using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartScoreJourneyHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();

    [Fact]
    public async Task ReturnsTheChartsJournalHistory()
    {
        var entries = new[]
        {
            new ScoreJournalEntry(DateTimeOffset.UnixEpoch, ScoreJournalEntry.ManualSource, UserId, ChartId,
                PhoenixScore.From(900000), null, false),
            new ScoreJournalEntry(DateTimeOffset.UnixEpoch.AddDays(30), ScoreJournalEntry.OfficialImportSource,
                UserId, ChartId, PhoenixScore.From(950000), null, false, MixEnum.Phoenix2)
        };
        var journal = new Mock<IScoreJournalRepository>();
        journal.Setup(j => j.GetChartHistories(UserId, It.Is<IEnumerable<Guid>>(ids => ids.Single() == ChartId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        var handler = BuildHandler(journal);

        var result = await handler.Handle(new GetChartScoreJourneyQuery(ChartId, UserId), CancellationToken.None);

        Assert.Equal(entries, result);
    }

    [Fact]
    public async Task DeniedAccessReturnsEmpty()
    {
        var journal = new Mock<IScoreJournalRepository>();
        var handler = BuildHandler(journal, hasAccess: false);

        var result = await handler.Handle(new GetChartScoreJourneyQuery(ChartId, UserId), CancellationToken.None);

        Assert.Empty(result);
        journal.Verify(j => j.GetChartHistories(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GetChartScoreJourneyHandler BuildHandler(Mock<IScoreJournalRepository> journal,
        bool hasAccess = true)
    {
        var access = new Mock<IUserAccessService>();
        access.Setup(a => a.HasAccessTo(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAccess);
        return new GetChartScoreJourneyHandler(journal.Object, new Mock<ICurrentUserAccessor>().Object,
            access.Object);
    }
}
