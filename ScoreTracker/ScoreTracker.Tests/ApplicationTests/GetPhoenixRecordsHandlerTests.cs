using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetPhoenixRecordsHandlerTests
{
    [Fact]
    public async Task DelegatesToRepositoryForRequestedUser()
    {
        var userId = Guid.NewGuid();
        var scores = new List<RecordedPhoenixScore>();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScores(userId, It.IsAny<CancellationToken>())).ReturnsAsync(scores);

        var handler = new GetPhoenixRecordsHandler(new Mock<IUserAccessService>().Object, records.Object);
        var result = await handler.Handle(new GetPhoenixRecordsQuery(userId), CancellationToken.None);

        Assert.Same(scores, result);
    }
}
