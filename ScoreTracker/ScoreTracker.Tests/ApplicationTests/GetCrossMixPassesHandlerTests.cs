using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetCrossMixPassesHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task ExcludesTheCurrentMixAndBrokenAttempts()
    {
        var passedP2 = Guid.NewGuid();
        var brokenP2 = Guid.NewGuid();
        var xxChart = new ChartBuilder().WithLevel(15).Build();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScores(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        records.Setup(r => r.GetRecordedScores(MixEnum.Phoenix2, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(passedP2, PhoenixScore.From(950000), null, false, DateTimeOffset.MinValue),
                new RecordedPhoenixScore(brokenP2, PhoenixScore.From(700000), null, true, DateTimeOffset.MinValue)
            });
        var xxAttempts = new Mock<IXXChartAttemptRepository>();
        xxAttempts.Setup(x => x.GetBestAttempts(UserId, MixEnum.XX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new BestXXChartAttempt(xxChart,
                    new XXChartAttempt(XXLetterGrade.A, false, null, DateTimeOffset.MinValue))
            });
        var handler = BuildHandler(records, xxAttempts);

        var result = await handler.Handle(new GetCrossMixPassesQuery(MixEnum.Phoenix, UserId),
            CancellationToken.None);

        Assert.Contains(passedP2, result);
        Assert.Contains(xxChart.Id, result);
        Assert.DoesNotContain(brokenP2, result);
        // The current mix's own records never contribute to "passed in another mix".
        records.Verify(r => r.GetRecordedScores(MixEnum.Phoenix, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeniedAccessReturnsEmpty()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var xxAttempts = new Mock<IXXChartAttemptRepository>();
        var handler = BuildHandler(records, xxAttempts, hasAccess: false);

        var result = await handler.Handle(new GetCrossMixPassesQuery(MixEnum.Phoenix, UserId),
            CancellationToken.None);

        Assert.Empty(result);
        records.Verify(r => r.GetRecordedScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GetCrossMixPassesHandler BuildHandler(Mock<IPhoenixRecordRepository> records,
        Mock<IXXChartAttemptRepository> xxAttempts, bool hasAccess = true)
    {
        // No default setups here — Moq's last-match-wins would override the tests'
        // specific setups, and loose mocks already return empty enumerables.
        var access = new Mock<IUserAccessService>();
        access.Setup(a => a.HasAccessTo(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasAccess);
        return new GetCrossMixPassesHandler(records.Object, xxAttempts.Object,
            new Mock<ICurrentUserAccessor>().Object, access.Object);
    }
}
