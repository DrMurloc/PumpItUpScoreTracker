using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class TournamentRoleSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly User _user = new UserBuilder().Build();

    private TournamentRoleSaga BuildSaga()
    {
        return new TournamentRoleSaga(_tournaments.Object, _currentUser.Object, FakeDateTime.At(Now).Object,
            _mediator.Object);
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

    [Fact]
    public async Task CreatingAnUnlistedTournamentSavesTheRecordAndGrantsTheCreatorHeadOrganizer()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();

        await BuildSaga().Handle(new CreateUnlistedTournamentCommand(tournamentId, "Bumble Bee Brawl"),
            CancellationToken.None);

        Expression<Func<TournamentRecord, bool>> expected = r =>
            r.Id == tournamentId && r.Name == "Bumble Bee Brawl" && !r.IsHighlighted && !r.IsMoM;
        _tournaments.Verify(t => t.CreateUnlistedTournament(It.Is(expected), It.IsAny<CancellationToken>()),
            Times.Once);
        _tournaments.Verify(t => t.SetRole(tournamentId, _user.Id, TournamentRole.HeadTournamentOrganizer,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatingAnUnlistedTournamentRequiresLogin()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);

        await Assert.ThrowsAsync<UserNotLoggedInException>(() => BuildSaga()
            .Handle(new CreateUnlistedTournamentCommand(Guid.NewGuid(), "Nope"), CancellationToken.None));

        _tournaments.Verify(
            t => t.CreateUnlistedTournament(It.IsAny<TournamentRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatingAnInviteRequiresHeadOrganizerOrAdmin()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.TournamentOrganizer));

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga().Handle(
            new CreateTournamentRoleInviteCommand(tournamentId, TournamentRole.Assistant, null),
            CancellationToken.None));

        _tournaments.Verify(t => t.CreateRoleInvite(It.IsAny<Guid>(), It.IsAny<TournamentRole>(),
            It.IsAny<DateTimeOffset?>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SiteAdminCanCreateInvitesWithoutHoldingARole()
    {
        LogIn(asAdmin: true);
        var tournamentId = Guid.NewGuid();
        var token = Guid.NewGuid();
        RolesAre(tournamentId);
        _tournaments.Setup(t => t.CreateRoleInvite(tournamentId, TournamentRole.Assistant, null, _user.Id,
            It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await BuildSaga().Handle(
            new CreateTournamentRoleInviteCommand(tournamentId, TournamentRole.Assistant, null),
            CancellationToken.None);

        Assert.Equal(token, result);
    }

    [Fact]
    public async Task HeadOrganizerReceivesTheMintedInviteToken()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var token = Guid.NewGuid();
        var expiry = Now + TimeSpan.FromDays(7);
        RolesAre(tournamentId,
            new UserTournamentRole(tournamentId, _user.Id, TournamentRole.HeadTournamentOrganizer));
        _tournaments.Setup(t => t.CreateRoleInvite(tournamentId, TournamentRole.TournamentOrganizer, expiry,
            _user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(token);

        var result = await BuildSaga().Handle(
            new CreateTournamentRoleInviteCommand(tournamentId, TournamentRole.TournamentOrganizer, expiry),
            CancellationToken.None);

        Assert.Equal(token, result);
    }

    [Fact]
    public async Task RedeemingAnUnknownTokenThrows()
    {
        LogIn();
        _tournaments.Setup(t => t.GetRoleInvite(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TournamentRoleInviteRecord?)null);

        await Assert.ThrowsAsync<TournamentInviteInvalidException>(() => BuildSaga()
            .Handle(new RedeemTournamentRoleInviteCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task RedeemingAnExpiredInviteThrows()
    {
        LogIn();
        var token = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        _tournaments.Setup(t => t.GetRoleInvite(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TournamentRoleInviteRecord(token, tournamentId, TournamentRole.Assistant,
                Now - TimeSpan.FromMinutes(1)));

        await Assert.ThrowsAsync<TournamentInviteInvalidException>(() => BuildSaga()
            .Handle(new RedeemTournamentRoleInviteCommand(token), CancellationToken.None));

        _tournaments.Verify(t => t.SetRole(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TournamentRole>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RedeemingGrantsTheInvitesRoleAndReturnsTheTournamentId()
    {
        LogIn();
        var token = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId);
        _tournaments.Setup(t => t.GetRoleInvite(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TournamentRoleInviteRecord(token, tournamentId, TournamentRole.Assistant,
                Now + TimeSpan.FromDays(1)));

        var result = await BuildSaga()
            .Handle(new RedeemTournamentRoleInviteCommand(token), CancellationToken.None);

        Assert.Equal(tournamentId, result);
        _tournaments.Verify(t => t.SetRole(tournamentId, _user.Id, TournamentRole.Assistant,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RedeemingNeverDowngradesAnExistingRole()
    {
        LogIn();
        var token = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId,
            new UserTournamentRole(tournamentId, _user.Id, TournamentRole.HeadTournamentOrganizer));
        _tournaments.Setup(t => t.GetRoleInvite(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TournamentRoleInviteRecord(token, tournamentId, TournamentRole.Assistant, null));

        var result = await BuildSaga()
            .Handle(new RedeemTournamentRoleInviteCommand(token), CancellationToken.None);

        Assert.Equal(tournamentId, result);
        _tournaments.Verify(t => t.SetRole(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TournamentRole>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
