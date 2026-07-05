using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Commands;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetTierListHandlerTests
{
    private static readonly Name ListName = Name.From("Difficulty");

    private static SongTierListEntry Entry()
    {
        return new SongTierListEntry(ListName, Guid.NewGuid(), TierListCategory.Medium, 1);
    }

    [Fact]
    public async Task ReturnsAllEntriesForTierList()
    {
        var entries = new List<SongTierListEntry> { Entry() };
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var handler = new GetTierListHandler(tierLists.Object);
        var result = await handler.Handle(new GetTierListQuery(ListName), CancellationToken.None);

        Assert.Equal(entries, result);
    }

    [Fact]
    public async Task Phoenix2WithNoEntriesFallsBackToPhoenixEntriesMarkedProvisional()
    {
        // Locked decision: an empty Phoenix2 tier list serves the Phoenix list, flagged
        // provisional, until Phoenix2 data accumulates.
        var phoenixEntries = new List<SongTierListEntry> { Entry(), Entry() };
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix2, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(phoenixEntries);

        var handler = new GetTierListHandler(tierLists.Object);
        var result = await handler.Handle(new GetTierListWithFallbackQuery(ListName, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.True(result.IsProvisionalFallback);
        Assert.Equal(phoenixEntries, result.Entries);
    }

    [Fact]
    public async Task Phoenix2WithItsOwnEntriesReturnsThemUnmarked()
    {
        var phoenix2Entries = new List<SongTierListEntry> { Entry() };
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix2, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(phoenix2Entries);

        var handler = new GetTierListHandler(tierLists.Object);
        var result = await handler.Handle(new GetTierListWithFallbackQuery(ListName, MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.False(result.IsProvisionalFallback);
        Assert.Equal(phoenix2Entries, result.Entries);
        tierLists.Verify(t => t.GetAllEntries(MixEnum.Phoenix, It.IsAny<Name>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PhoenixWithNoEntriesDoesNotFallBackAnywhere()
    {
        // The fallback is a Phoenix2-only affordance; an empty Phoenix list is just empty.
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());

        var handler = new GetTierListHandler(tierLists.Object);
        var result = await handler.Handle(new GetTierListWithFallbackQuery(ListName), CancellationToken.None);

        Assert.False(result.IsProvisionalFallback);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task GetTierListQueryAppliesTheSameFallbackEntriesWithoutTheFlag()
    {
        // Existing callers of the plain query transparently see the provisional entries.
        var phoenixEntries = new List<SongTierListEntry> { Entry() };
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix2, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        tierLists.Setup(t => t.GetAllEntries(MixEnum.Phoenix, ListName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(phoenixEntries);

        var handler = new GetTierListHandler(tierLists.Object);
        var result = await handler.Handle(new GetTierListQuery(ListName, MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(phoenixEntries, result.ToList());
    }
}
