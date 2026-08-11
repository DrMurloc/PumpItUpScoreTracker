using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The handler is a dispatch onto the journal's grouped count; the counting rule itself is
///     proven against a real database in Tests.Integration, since a mocked repository here
///     would only be asserting the mock.
/// </summary>
public sealed class GetPlayerChartPlayCountsHandlerTests
{
    [Fact]
    public async Task ThePlayerAndMixReachTheJournalUnchanged()
    {
        var user = Guid.NewGuid();
        var chart = Guid.NewGuid();
        var journal = new Mock<IScoreJournalRepository>();
        journal.Setup(j => j.GetChartPlayCounts(user, MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [chart] = 4 });

        var result = await new GetPlayerChartPlayCountsHandler(journal.Object)
            .Handle(new GetPlayerChartPlayCountsQuery(user, MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(4, result[chart]);
        journal.Verify(j => j.GetChartPlayCounts(user, MixEnum.Phoenix2, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
