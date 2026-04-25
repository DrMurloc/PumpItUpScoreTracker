using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetTierListHandlerTests
{
    [Fact]
    public async Task DelegatesToRepository()
    {
        var entries = new List<SongTierListEntry>();
        var name = Name.From("Difficulty");
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(name, It.IsAny<CancellationToken>())).ReturnsAsync(entries);

        var handler = new GetTierListHandler(tierLists.Object);
        var result = await handler.Handle(new GetTierListQuery(name), CancellationToken.None);

        Assert.Same(entries, result);
    }
}
