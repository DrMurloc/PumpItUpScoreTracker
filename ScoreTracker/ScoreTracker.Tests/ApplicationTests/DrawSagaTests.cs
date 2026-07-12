using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Randomizer.Application;
using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.Randomizer.Contracts.Commands;
using ScoreTracker.Randomizer.Contracts.Events;
using ScoreTracker.Randomizer.Contracts.Queries;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DrawSagaTests
{
    private readonly Mock<IDrawRepository> _draws = new();
    private readonly Mock<IRandomizerRepository> _settings = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly User _user = new UserBuilder().Build();

    private DrawSaga BuildSaga()
    {
        return new DrawSaga(_draws.Object, _settings.Object, _currentUser.Object, _mediator.Object);
    }

    private void LogIn(bool asAdmin = false)
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(_user);
        _currentUser.SetupGet(c => c.IsLoggedInAsAdmin).Returns(asAdmin);
    }

    private void RolesAre(Guid tournamentId, params UserTournamentRole[] roles)
    {
        _mediator.Setup(m => m.Send(It.Is<GetTournamentRolesQuery>(q => q.TournamentId == tournamentId),
            It.IsAny<CancellationToken>())).ReturnsAsync(roles);
    }

    private static DrawDto Draw(Guid? tournamentId = null)
    {
        return new DrawDto(Guid.NewGuid(), Guid.NewGuid(), MixEnum.Phoenix, tournamentId,
            Array.Empty<DrawCardDto>());
    }

    [Fact]
    public async Task PersonalDrawIsCreatedForTheCurrentUserAndPublishesDrawUpdated()
    {
        LogIn();
        var chartIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var draw = Draw();
        _draws.Setup(d => d.ReplaceDraw(_user.Id, null, MixEnum.Phoenix, chartIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draw);

        var result = await BuildSaga()
            .Handle(new CreateDrawCommand(null, MixEnum.Phoenix, chartIds), CancellationToken.None);

        Assert.Equal(draw, result);
        _mediator.Verify(m => m.Publish(It.Is<DrawUpdatedEvent>(e => e.DrawId == draw.Id && e.Slug == draw.Slug),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TournamentDrawRequiresARoleOnTheTournament()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId);

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga().Handle(
            new CreateDrawCommand(tournamentId, MixEnum.Phoenix, Array.Empty<Guid>()), CancellationToken.None));

        _draws.Verify(d => d.ReplaceDraw(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<MixEnum>(),
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssistantsCanOperateTournamentDraws()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        var draw = Draw(tournamentId);
        _draws.Setup(d => d.ReplaceDraw(null, tournamentId, MixEnum.Phoenix2, It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        var result = await BuildSaga().Handle(
            new CreateDrawCommand(tournamentId, MixEnum.Phoenix2, new[] { Guid.NewGuid() }), CancellationToken.None);

        Assert.Equal(draw, result);
    }

    [Fact]
    public async Task SettingACardStateAuthorizesAgainstTheDrawsContextAndPublishes()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        var pullId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await BuildSaga().Handle(new SetDrawCardStateCommand(draw.Id, pullId, DrawCardState.Vetoed),
            CancellationToken.None);

        _draws.Verify(d => d.SetCardState(draw.Id, pullId, DrawCardState.Vetoed, It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(m => m.Publish(It.Is<DrawUpdatedEvent>(e => e.DrawId == draw.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearingVetoedCardsReturnsTheCompactedDraw()
    {
        LogIn();
        var draw = Draw();
        var compacted = draw with { Cards = Array.Empty<DrawCardDto>() };
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);
        _draws.Setup(d => d.ClearVetoed(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(compacted);

        var result = await BuildSaga().Handle(new ClearVetoedCardsCommand(draw.Id), CancellationToken.None);

        Assert.Equal(compacted, result);
        _mediator.Verify(m => m.Publish(It.IsAny<DrawUpdatedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActiveDrawQueryReturnsNullWhenLoggedOut()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        var result = await BuildSaga().Handle(new GetActiveDrawQuery(null), CancellationToken.None);

        Assert.Null(result);
        _draws.Verify(d => d.GetActiveDraw(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TournamentSettingsWritesRequireAnOrganizerRole()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga().Handle(
            new SaveTournamentRandomSettingsCommand(tournamentId, "night settings", new RandomSettings()),
            CancellationToken.None));

        _settings.Verify(s => s.SaveTournamentSettings(It.IsAny<Guid>(), It.IsAny<Name>(),
            It.IsAny<RandomSettings>(), It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OrganizersSaveTournamentSettings()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var settings = new RandomSettings();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.TournamentOrganizer));

        await BuildSaga().Handle(
            new SaveTournamentRandomSettingsCommand(tournamentId, "night settings", settings, MixEnum.Phoenix2),
            CancellationToken.None);

        _settings.Verify(s => s.SaveTournamentSettings(tournamentId,
            It.Is<Name>(n => (string)n == "night settings"), settings, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShareLinkMintsThroughTheRepositoryForTheCurrentUser()
    {
        LogIn();
        var token = Guid.NewGuid();
        _settings.Setup(s => s.EnsureShareToken(_user.Id, It.Is<Name>(n => (string)n == "favorites"),
            It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await BuildSaga()
            .Handle(new CreateSettingsShareLinkCommand("favorites"), CancellationToken.None);

        Assert.Equal(token, result);
    }
}
