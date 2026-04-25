using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TournamentHandlerTests
{
    [Fact]
    public async Task SaveDelegatesToRepository()
    {
        var tournament = new TournamentConfiguration(new ScoringConfiguration());
        var tournaments = new Mock<ITournamentRepository>();

        var handler = new TournamentHandler(tournaments.Object);
        await handler.Handle(new SaveTournamentCommand(tournament), CancellationToken.None);

        tournaments.Verify(t => t.CreateOrSaveTournament(tournament, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllDelegatesToRepository()
    {
        var records = new List<TournamentRecord>();
        var tournaments = new Mock<ITournamentRepository>();
        tournaments.Setup(t => t.GetAllTournaments(It.IsAny<CancellationToken>())).ReturnsAsync(records);

        var handler = new TournamentHandler(tournaments.Object);
        var result = await handler.Handle(new GetAllTournamentsQuery(), CancellationToken.None);

        Assert.Same(records, result);
    }

    [Fact]
    public async Task GetSingleDelegatesToRepository()
    {
        var id = Guid.NewGuid();
        var tournament = new TournamentConfiguration(new ScoringConfiguration());
        var tournaments = new Mock<ITournamentRepository>();
        tournaments.Setup(t => t.GetTournament(id, It.IsAny<CancellationToken>())).ReturnsAsync(tournament);

        var handler = new TournamentHandler(tournaments.Object);
        var result = await handler.Handle(new GetTournamentQuery(id), CancellationToken.None);

        Assert.Equal(tournament, result);
    }
}
