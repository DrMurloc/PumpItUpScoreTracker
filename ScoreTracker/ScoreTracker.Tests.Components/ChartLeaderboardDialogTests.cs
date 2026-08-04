using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Moq;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Renders the dialog for the same reason the hero has a render test: Razor accepts an
///     invented parameter at compile time and throws on first render, so a component nothing
///     ever renders is a component nothing ever checks.
/// </summary>
public sealed class ChartLeaderboardDialogTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserReader> _readers = new();

    public ChartLeaderboardDialogTests()
    {
        var chart = TestChart();
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        // The world board reads the World COMMUNITY now, not every score on the chart.
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetCompetitivePlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        // DifficultyBubble reads scoring levels through this; an unstubbed mock hands back null
        // and the bubble dereferences it before the dialog's own markup ever renders.
        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<Guid, double>)new Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
        // UserLabel needs the whole user for its flag, so the rows resolve them through here.
        _readers.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        Services.AddSingleton(_readers.Object);
        // UserLabel resolves its country image through this.
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetCountryImage(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Uri("https://example.invalid/flag.png"));
        Services.AddSingleton(repo.Object);
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        ChartId = chart.Id;
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private Guid ChartId { get; }

    [Fact]
    public void AnEmptyBoardNamesWhatWouldFillItRatherThanRenderingNothing()
    {
        var dialog = RenderDialog();

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-empty']")));
        // Every scope stays reachable — an unavailable one greys rather than disappearing.
        Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-World']"));
        Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-CompetitivePeers']"));
    }

    [Fact]
    public void TiedScoresShareAPlaceAndTheNextPlaceSkipsTheTieBlock()
    {
        // Five perfect games are five #1s, and the best score under them is #6 — not #2.
        var perfect = Enumerable.Range(0, 5)
            .Select(i => Score(1_000_000, When.AddDays(-i)))
            .ToArray();
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(perfect.Append(Score(994_000, When)).ToArray());

        var dialog = RenderDialog();

        dialog.WaitForAssertion(() =>
        {
            var places = dialog.FindAll(".weekly-lb-place").Select(e => e.TextContent.Trim()).ToArray();
            Assert.Equal(new[] { "#1", "#1", "#1", "#1", "#1", "#6" }, places);
        });
    }

    [Fact]
    public void ATieOrdersOldestFirst()
    {
        // Whoever got there first reads first — the only ordering a tie has a claim to.
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Score(1_000_000, When, "LATEST"),
                Score(1_000_000, When.AddYears(-2), "EARLIEST")
            });

        var dialog = RenderDialog();

        dialog.WaitForAssertion(() =>
        {
            var names = dialog.FindAll(".weekly-lb-user").Select(e => e.TextContent.Trim()).ToArray();
            Assert.StartsWith("EARLIEST", names.First());
        });
    }

    [Fact]
    public void RowsRenderTheAvatarAndTheFlagWhenTheUserResolves()
    {
        // Every other fact here mocks zero users, so an empty map reads as correct and a
        // dropped assignment looks exactly like a board of players with no country set.
        var score = Score(994_000, When, "MIDNIGHT");
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { score });
        _readers.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User(score.UserId, Name.From("MIDNIGHT"), true, null,
                    new Uri("https://example.invalid/avatar.png"), Name.From("United States of America"),
                    false, When)
            });

        var dialog = RenderDialog();

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".sbd-avatar")));
        Assert.NotEmpty(dialog.FindAll(".user-label"));
    }

    [Fact]
    public void TheCommunityPickerStaysHiddenWithoutTwoCommunities()
    {
        // A control with one choice is furniture, not a control (D19). Signed out here, so
        // there are none at all — the strictest version of the same rule.
        var dialog = RenderDialog();

        // Assert the dialog actually rendered before asserting on an absence, or the test
        // passes on a blank tree.
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-World']")));
        Assert.Empty(dialog.FindAll("[data-testid='cld-community-picker']"));
    }

    [Fact]
    public void EveryBoardReadCarriesTheMixItWasGiven()
    {
        // Phoenix and Phoenix 2 share chart ids, so a board that drops the mix reads as a
        // full, plausible leaderboard — of the wrong game.
        var dialog = RenderDialog(MixEnum.Phoenix2, ChartLeaderboardScopes.LeaderboardScope.CompetitivePeers);

        dialog.WaitForAssertion(() => VerifyWorldBoardRead(MixEnum.Phoenix2));
        _mediator.Verify(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _mediator.Verify(m => m.Send(It.Is<GetCompetitivePlayersQuery>(q => q.Mix == MixEnum.Phoenix2),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public void ChangingMixOnTheSameChartRebuildsTheBoard()
    {
        // The sessions page keeps one dialog and swaps its mix as the hero session changes,
        // so a load keyed on the chart alone serves the previous mix's board back.
        RenderComponent<MudDialogProvider>();
        var dialog = RenderComponent<ChartLeaderboardDialog>(p => p
            .Add(c => c.Visible, true)
            .Add(c => c.ChartId, ChartId)
            .Add(c => c.Mix, MixEnum.Phoenix));
        dialog.WaitForAssertion(() => VerifyWorldBoardRead(MixEnum.Phoenix));

        dialog.SetParametersAndRender(p => p.Add(c => c.Mix, MixEnum.Phoenix2));

        dialog.WaitForAssertion(() => VerifyWorldBoardRead(MixEnum.Phoenix2));
    }

    private void VerifyWorldBoardRead(MixEnum mix)
    {
        _mediator.Verify(m => m.Send(It.Is<GetPhoenixRecordsForCommunityQuery>(q => q.Mix == mix),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>Inline MudDialogs render through the provider, so the fragment hosts both.</summary>
    private IRenderedFragment RenderDialog(MixEnum mix = MixEnum.Phoenix,
        ChartLeaderboardScopes.LeaderboardScope scope = ChartLeaderboardScopes.LeaderboardScope.World)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartLeaderboardDialog>(1);
            builder.AddAttribute(2, nameof(ChartLeaderboardDialog.Visible), true);
            builder.AddAttribute(3, nameof(ChartLeaderboardDialog.ChartId), ChartId);
            builder.AddAttribute(4, nameof(ChartLeaderboardDialog.Mix), mix);
            builder.AddAttribute(5, nameof(ChartLeaderboardDialog.InitialScope), scope);
            builder.CloseComponent();
        });
    }

    /// <summary>
    ///     The reported bug: a rival who is also a clubmate read as a plain clubmate on this board,
    ///     because its Row model had no rival flag at all. Asserting the class the stylesheet keys
    ///     on, not the presence of a row.
    /// </summary>
    [Fact]
    public void ARivalWhoIsAlsoAClubmateGetsTheSegmentedRow()
    {
        var rivalAndClubmate = Guid.NewGuid();
        var clubmateOnly = Guid.NewGuid();
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("ME"), true, null,
            new Uri("https://example.invalid/me.png"), null));
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityOverviewRecord(Name.From("Crew"), CommunityPrivacyType.Public, 2, false) });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { rivalAndClubmate, clubmateOnly });
        _mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RivalSubject(Guid.NewGuid(), rivalAndClubmate, null, "TRIGGER", null, false,
                    RivalCapabilities.LiveScores, When)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ScoreFor(rivalAndClubmate, 990_000, "TRIGGER"),
                ScoreFor(clubmateOnly, 980_000, "VALEX")
            });

        var cut = RenderDialog();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll(".weekly-lb-row");
            Assert.Equal(2, rows.Count);
            Assert.Contains("is-both", rows[0].ClassName);
            Assert.Contains("weekly-lb-community", rows[1].ClassName);
            Assert.DoesNotContain("is-both", rows[1].ClassName);
        });
    }

    private static UserPhoenixScore ScoreFor(Guid userId, int score, string name) =>
        new(userId, Guid.NewGuid(), Name.From(name), PhoenixScore.From(score),
            PhoenixPlate.PerfectGame, false, true, When);

    private static UserPhoenixScore Score(int score, DateTimeOffset recordedAt, string name = "PLAYER")
    {
        return new UserPhoenixScore(Guid.NewGuid(), Guid.NewGuid(), Name.From(name),
            PhoenixScore.From(score), PhoenixPlate.PerfectGame, false, true, recordedAt);
    }

    private static readonly DateTimeOffset When = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private static Chart TestChart()
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ScoreTracker.SharedKernel.Enums.ChartType.Single, DifficultyLevel.From(21),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }
}
