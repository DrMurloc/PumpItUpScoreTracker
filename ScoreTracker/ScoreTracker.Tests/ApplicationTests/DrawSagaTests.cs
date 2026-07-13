using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IBotClient> _bot = new();
    private readonly User _user = new UserBuilder().Build();

    private DrawSaga BuildSaga()
    {
        return new DrawSaga(_draws.Object, _settings.Object, _currentUser.Object, _mediator.Object,
            _charts.Object, _bot.Object);
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
        _draws.Setup(d => d.ReplacePersonalDraw(_user.Id, MixEnum.Phoenix, chartIds, It.IsAny<CancellationToken>()))
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
            new CreateDrawCommand(tournamentId, MixEnum.Phoenix, Array.Empty<Guid>(), "R1"),
            CancellationToken.None));

        _draws.Verify(d => d.CreateTournamentDraw(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<MixEnum>(),
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TournamentMatchesRequireAName()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));

        await Assert.ThrowsAsync<RandomizerException>(() => BuildSaga().Handle(
            new CreateDrawCommand(tournamentId, MixEnum.Phoenix, new[] { Guid.NewGuid() }),
            CancellationToken.None));

        _draws.Verify(d => d.CreateTournamentDraw(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<MixEnum>(),
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssistantsCreateNamedTournamentMatches()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        var draw = Draw(tournamentId);
        _draws.Setup(d => d.CreateTournamentDraw(tournamentId, "R1 - Ada vs Kei", MixEnum.Phoenix2,
            It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        var result = await BuildSaga().Handle(
            new CreateDrawCommand(tournamentId, MixEnum.Phoenix2, new[] { Guid.NewGuid() }, "R1 - Ada vs Kei"),
            CancellationToken.None);

        Assert.Equal(draw, result);
    }

    [Fact]
    public async Task RedrawRefillsTheDrawUnderItsContextAuthorization()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        var chartIds = new[] { Guid.NewGuid() };
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);
        _draws.Setup(d => d.RedrawCards(draw.Id, MixEnum.Phoenix, chartIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draw);

        var result = await BuildSaga()
            .Handle(new RedrawCardsCommand(draw.Id, MixEnum.Phoenix, chartIds), CancellationToken.None);

        Assert.Equal(draw, result);
        _mediator.Verify(m => m.Publish(It.Is<DrawUpdatedEvent>(e => e.DrawId == draw.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletingAMatchRequiresARoleAndPublishes()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId);
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga()
            .Handle(new DeleteDrawCommand(draw.Id), CancellationToken.None));

        _draws.Verify(d => d.DeleteDraw(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssistantsCannotDeleteTournamentMatches()
    {
        // Round 8: delete is organizer territory — assistants only run the tablet.
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga()
            .Handle(new DeleteDrawCommand(draw.Id), CancellationToken.None));

        _draws.Verify(d => d.DeleteDraw(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OrganizersDeleteMatchesAndPublish()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.TournamentOrganizer));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await BuildSaga().Handle(new DeleteDrawCommand(draw.Id), CancellationToken.None);

        _draws.Verify(d => d.DeleteDraw(draw.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Publish(It.Is<DrawUpdatedEvent>(e => e.DrawId == draw.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OrganizersRenameAMatchAndTheSlugNeverMoves()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.TournamentOrganizer));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await BuildSaga().Handle(new RenameDrawCommand(draw.Id, "  Grand Final  "), CancellationToken.None);

        _draws.Verify(d => d.RenameDraw(draw.Id, "Grand Final", It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Publish(It.Is<DrawUpdatedEvent>(e => e.DrawId == draw.Id && e.Slug == draw.Slug),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssistantsCannotRenameMatches()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga()
            .Handle(new RenameDrawCommand(draw.Id, "Grand Final"), CancellationToken.None));

        _draws.Verify(d => d.RenameDraw(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PersonalDrawsCannotBeRenamed()
    {
        LogIn();
        var draw = Draw();
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<RandomizerException>(() => BuildSaga()
            .Handle(new RenameDrawCommand(draw.Id, "My Draw"), CancellationToken.None));
    }

    [Fact]
    public async Task RenamingToABlankNameIsRejected()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<RandomizerException>(() => BuildSaga()
            .Handle(new RenameDrawCommand(draw.Id, "   "), CancellationToken.None));

        _draws.Verify(d => d.RenameDraw(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task PushSendsThePlayOrderCardToTheConfiguredChannel()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));

        var district = new ChartBuilder().WithSongName("District 1").WithLevel(17).Build();
        var achluoias = new ChartBuilder().WithSongName("Achluoias").WithType(ChartType.Double).WithLevel(24).Build();
        var gargoyle = new ChartBuilder().WithSongName("Gargoyle").WithLevel(19).Build();
        var draw = new DrawDto(Guid.NewGuid(), Guid.NewGuid(), MixEnum.Phoenix, tournamentId, new[]
        {
            new DrawCardDto(Guid.NewGuid(), district.Id, 1, DrawCardState.None),
            new DrawCardDto(Guid.NewGuid(), achluoias.Id, 2, DrawCardState.Vetoed),
            new DrawCardDto(Guid.NewGuid(), gargoyle.Id, 3, DrawCardState.Protected)
        }, "R1 - Ada vs Kei");
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);
        _mediator.Setup(m => m.Send(It.Is<GetTournamentDiscordChannelQuery>(q => q.TournamentId == tournamentId),
            It.IsAny<CancellationToken>())).ReturnsAsync((ulong?)42);
        _mediator.Setup(m => m.Send(It.Is<GetTournamentQuery>(q => q.TournamentId == tournamentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TournamentConfiguration(new ScoringConfiguration()) { Name = "Bumble Bee Brawl" });
        _charts.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { district, achluoias, gargoyle });
        RichBotMessage? sent = null;
        ulong[]? channels = null;
        _bot.Setup(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(), It.IsAny<IEnumerable<ulong>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RichBotMessage>, IEnumerable<ulong>, CancellationToken>((messages, channelIds, _) =>
            {
                sent = messages.Single();
                channels = channelIds.ToArray();
            })
            .Returns(Task.CompletedTask);

        await BuildSaga().Handle(new PushDrawToDiscordCommand(draw.Id), CancellationToken.None);

        Assert.Equal(42UL, Assert.Single(channels!));
        Assert.NotNull(sent);
        Assert.Contains("Bumble Bee Brawl", sent!.Header!.Markdown);
        Assert.Contains("R1 - Ada vs Kei", sent.Header.Markdown);

        // Played charts lead in play order with jackets and their original badge numbers;
        // the veto strip trails them, struck and art-free.
        var blocks = sent.Blocks.ToList();
        var sections = blocks.OfType<RichBotSection>().ToArray();
        Assert.Equal(2, sections.Length);
        Assert.Contains("`1`", sections[0].Markdown);
        Assert.Contains("District 1", sections[0].Markdown);
        Assert.NotNull(sections[0].Thumbnail);
        Assert.Contains("`3`", sections[1].Markdown);
        Assert.Contains("HELD", sections[1].Markdown);
        var vetoes = blocks.OfType<RichBotText>().First(t => t.Markdown.Contains("VETOED"));
        Assert.Contains("~~Achluoias~~", vetoes.Markdown);
        Assert.True(blocks.IndexOf(vetoes) > blocks.IndexOf(sections[1]));
        Assert.Contains("2 to play · 1 vetoed · 1 held", blocks.OfType<RichBotText>().Last().Markdown);
        Assert.Contains(draw.Slug.ToString(), Assert.Single(sent.Links).Url.ToString());
    }

    [Fact]
    public async Task PushWithoutAConfiguredChannelSendsNothing()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId, new UserTournamentRole(tournamentId, _user.Id, TournamentRole.Assistant));
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);
        _mediator.Setup(m => m.Send(It.IsAny<GetTournamentDiscordChannelQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ulong?)null);

        await Assert.ThrowsAsync<RandomizerException>(() => BuildSaga()
            .Handle(new PushDrawToDiscordCommand(draw.Id), CancellationToken.None));

        _bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(), It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PersonalDrawsCannotPushToDiscord()
    {
        LogIn();
        var draw = Draw();
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<RandomizerException>(() => BuildSaga()
            .Handle(new PushDrawToDiscordCommand(draw.Id), CancellationToken.None));
    }

    [Fact]
    public async Task PushRequiresAStaffRole()
    {
        LogIn();
        var tournamentId = Guid.NewGuid();
        var draw = Draw(tournamentId);
        RolesAre(tournamentId);
        _draws.Setup(d => d.GetDraw(draw.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draw);

        await Assert.ThrowsAsync<NotAuthorizedException>(() => BuildSaga()
            .Handle(new PushDrawToDiscordCommand(draw.Id), CancellationToken.None));

        _bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(), It.IsAny<IEnumerable<ulong>>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
