using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Moq;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
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
public sealed class ChartLeaderboardScopesTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserReader> _readers = new();

    public ChartLeaderboardScopesTests()
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
        _mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PeerVoice>());
        // Every chart view asks whether it carries a limbo board. Unstubbed this hands back null
        // and the component dereferences it during load, which takes out every test in the file
        // rather than just the limbo ones.
        _mediator.Setup(m => m.Send(It.IsAny<GetLimboChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<Guid>)new HashSet<Guid>());
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
    public void RowsRenderTheAvatarAndTheCountryWashWhenTheUserResolves()
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

        // A row the site can name is drawn entirely by the label — avatar inside it, so the
        // country wash runs under both. The standalone .sbd-avatar is what an Official-scope
        // row keeps, and this row is not one.
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll(".user-label img.user-label-avatar")));
        Assert.NotEmpty(dialog.FindAll(".user-label.has-flag"));
        Assert.Empty(dialog.FindAll(".sbd-avatar"));
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
        // full, plausible leaderboard — of the wrong game. Signed in: a band is somebody's.
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("ME"), true, null,
            new Uri("https://piu.test/me.png"), null));
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
        // A host can keep one board and swap its mix — the details dialogue does exactly that as
        // its chart changes — so a load keyed on the chart alone serves the previous mix's board
        // back (origin/main f00dd27e).
        RenderComponent<MudDialogProvider>();
        var dialog = RenderComponent<ChartLeaderboardScopes>(p => p
            .Add(c => c.Active, true)
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
            builder.OpenComponent<ChartLeaderboardScopes>(1);
            builder.AddAttribute(2, nameof(ChartLeaderboardScopes.Active), true);
            builder.AddAttribute(3, nameof(ChartLeaderboardScopes.ChartId), ChartId);
            builder.AddAttribute(4, nameof(ChartLeaderboardScopes.Mix), mix);
            builder.AddAttribute(5, nameof(ChartLeaderboardScopes.InitialScope), scope);
            builder.CloseComponent();
        });
    }

    /// <summary>
    ///     Another player's sessions page opens THEIR band from a peer line: the host names the
    ///     subject and the competitive read is theirs, signed in or not.
    /// </summary>
    [Fact]
    public void TheCompetitiveBoardIsTheSubjectsBandWhenAHostNamesOne()
    {
        var subject = Guid.NewGuid();
        var dialog = Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ChartLeaderboardScopes>(1);
            builder.AddAttribute(2, nameof(ChartLeaderboardScopes.Active), true);
            builder.AddAttribute(3, nameof(ChartLeaderboardScopes.ChartId), ChartId);
            builder.AddAttribute(4, nameof(ChartLeaderboardScopes.Mix), MixEnum.Phoenix);
            builder.AddAttribute(5, nameof(ChartLeaderboardScopes.InitialScope),
                ChartLeaderboardScopes.LeaderboardScope.CompetitivePeers);
            builder.AddAttribute(6, nameof(ChartLeaderboardScopes.Subject), subject);
            builder.CloseComponent();
        });

        dialog.WaitForAssertion(() => _mediator.Verify(
            m => m.Send(It.Is<GetCompetitivePlayersQuery>(q => q.Subject == subject), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce));
        Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-scope-CompetitivePeers']").ClassName);
    }

    /// <summary>
    ///     Logged out with no subject named, the Competitive chip is off rather than a board that
    ///     reaches for a user who is not there.
    /// </summary>
    [Fact]
    public void LoggedOutWithNoSubjectTheCompetitiveChipIsOff()
    {
        var dialog = RenderDialog(MixEnum.Phoenix, ChartLeaderboardScopes.LeaderboardScope.CompetitivePeers);

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-World']")));
        var chip = dialog.Find("[data-testid='cld-scope-CompetitivePeers']");
        Assert.True(chip.HasAttribute("disabled"));
        Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-scope-World']").ClassName);
        _mediator.Verify(m => m.Send(It.IsAny<GetCompetitivePlayersQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            .ReturnsAsync(new[] { new CommunityOverviewRecord(Name.From("Crew"), CommunityPrivacyType.Public, 2, false, Guid.NewGuid()) });
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

    /// <summary>
    ///     The mirror knows which board tags link to site accounts, but a PRIVATE account never
    ///     published that link. Their row stays a bare board tag: no site username, and no glow,
    ///     because a glow says "this tag is someone you know" as loudly as a name does.
    /// </summary>
    [Fact]
    public void APrivateAccountsLinkIsNotSurfacedOnTheOfficialBoard()
    {
        var publicUser = Guid.NewGuid();
        var privateUser = Guid.NewGuid();
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(Guid.NewGuid(), Name.From("ME"), true, null,
            new Uri("https://example.invalid/me.png"), null));
        // Both are clubmates, so both would glow if the link were honoured for either.
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityOverviewRecord(Name.From("Crew"), CommunityPrivacyType.Public, 2, false, Guid.NewGuid()) });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { publicUser, privateUser });
        _readers.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User(publicUser, Name.From("OPENBOOK"), true, null, new Uri("https://example.invalid/a.png"), null),
                new User(privateUser, Name.From("HIDDEN"), false, null, new Uri("https://example.invalid/b.png"), null)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialChartBoardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialChartBoardRecord(When, new[]
            {
                new OfficialChartBoardEntryRecord(1,
                    new OfficialPlayerRecord(1, "OPENBOOK#1", new Uri("https://example.invalid/o.png"), publicUser), 999_000),
                new OfficialChartBoardEntryRecord(2,
                    new OfficialPlayerRecord(2, "HIDDEN#2", new Uri("https://example.invalid/h.png"), privateUser), 998_000)
            }));

        var cut = RenderDialog(scope: ChartLeaderboardScopes.LeaderboardScope.Official);

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll(".weekly-lb-row");
            Assert.Equal(2, rows.Count);
            // Both are named by their board tag, never their site username.
            Assert.Contains("OPENBOOK#1", rows[0].TextContent);
            Assert.Contains("HIDDEN#2", rows[1].TextContent);
            Assert.DoesNotContain("HIDDEN", rows[1].ClassName);
            // The public link still glows; the private one is treated as an unlinked tag.
            Assert.Contains("weekly-lb-community", rows[0].ClassName);
            Assert.DoesNotContain("weekly-lb-community", rows[1].ClassName);
        });
    }

    [Fact]
    public void AnUnflaggedChartShowsNoLimboChipAtAll()
    {
        var dialog = RenderDialog();

        // The one scope that hides rather than greys: a permanently disabled chip on every chart
        // in the game is furniture nobody asked for.
        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-World']")));
        Assert.Empty(dialog.FindAll("[data-testid='cld-scope-LowestPassing']"));
    }

    [Fact]
    public void AFlaggedChartShowsTheLimboChip()
    {
        GivenAFlaggedChart();

        var dialog = RenderDialog();

        dialog.WaitForAssertion(() =>
            Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-LowestPassing']")));
    }

    [Fact]
    public async Task TheLimboBoardRanksTheLowestPassFirst()
    {
        GivenAFlaggedChart();
        _mediator.Setup(m => m.Send(It.IsAny<GetLowestPassingScoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Score(640_500, When, "TRIER"),
                Score(312_004, When, "LOWBALLER"),
                Score(444_444, When, "BREAKER")
            });
        var dialog = RenderDialog();
        dialog.WaitForAssertion(() =>
            Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-LowestPassing']")));

        // Awaited, never Click(): the synchronous helper posts the event and returns, so the
        // assertion reads the pre-click render (bunit-apex-click trap).
        await dialog.Find("[data-testid='cld-scope-LowestPassing']").ClickAsync(new MouseEventArgs());

        dialog.WaitForAssertion(() =>
        {
            var names = dialog.FindAll(".weekly-lb-user").Select(e => e.TextContent.Trim()).ToArray();
            Assert.Equal(new[] { "LOWBALLER", "BREAKER", "TRIER" }, names);
            // Place still counts up from the top of the board — the board just runs the other way.
            Assert.Equal(new[] { "#1", "#2", "#3" },
                dialog.FindAll(".weekly-lb-place").Select(e => e.TextContent.Trim()).ToArray());
        });
    }

    // ------------------------------------------------------------------ the Peers chip (D40)

    [Fact]
    public void OnPhoenix1ThePeersChipIsTheCompetitiveBoardAndNoSubRowAppears()
    {
        SignedIn();
        var dialog = RenderDialog(MixEnum.Phoenix, ChartLeaderboardScopes.LeaderboardScope.CompetitivePeers);

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-scope-CompetitivePeers']")));
        Assert.Contains("Competitive Peers", dialog.Find("[data-testid='cld-scope-CompetitivePeers']").TextContent);
        Assert.Empty(dialog.FindAll("[data-testid='cld-peer-picker']"));
        // PUMBILITY peers is never a chip of its own.
        Assert.Empty(dialog.FindAll("[data-testid='cld-scope-PumbilityPeers']"));
    }

    [Fact]
    public void OnPhoenix2ThePeersChipOpensOnCompetitiveWithASubRowToSwitchPools()
    {
        SignedIn();
        var dialog = RenderDialog(MixEnum.Phoenix2, ChartLeaderboardScopes.LeaderboardScope.CompetitivePeers);

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-peer-picker']")));
        Assert.Contains("Peers", dialog.Find("[data-testid='cld-scope-CompetitivePeers']").TextContent);
        Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-scope-CompetitivePeers']").ClassName);
        // Cold, the sub-row is on Competitive — "do competitive for now" — and the PUMBILITY read is untouched.
        Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-peers-competitive']").ClassName);
        Assert.DoesNotContain("cld-chip-on", dialog.Find("[data-testid='cld-peers-pumbility']").ClassName);
        _mediator.Verify(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ThePumbilityPeersBoardIsTheWorldBoardCutToYourPeersPrivateOnesKeptAndYouOnIt()
    {
        var me = Guid.NewGuid();
        SignedInAs(me);
        var peer = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                ScoreFor(peer, 990_000, "PEER"),
                new UserPhoenixScore(hidden, ChartId, Name.From("Anonymous"), PhoenixScore.From(985_000), PhoenixPlate.SuperbGame, false, false, When),
                ScoreFor(stranger, 999_000, "STRANGER"),
                ScoreFor(me, 970_000, "ME")
            });
        // The peer read never names the viewer (D31) — the board puts them on it anyway.
        _mediator.Setup(m => m.Send(It.Is<GetPumbilityPeersQuery>(q => q.Mix == MixEnum.Phoenix2 && q.ChartType == ScoreTracker.SharedKernel.Enums.ChartType.Single),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PeerVoice.Account(peer), PeerVoice.Account(hidden) });

        // A host that means the PUMBILITY board passes it as the initial scope — a peers-page card does.
        var dialog = RenderDialog(MixEnum.Phoenix2, ChartLeaderboardScopes.LeaderboardScope.PumbilityPeers);

        dialog.WaitForAssertion(() =>
        {
            var names = dialog.FindAll(".weekly-lb-user").Select(e => e.TextContent.Trim()).ToArray();
            Assert.Equal(new[] { "PEER", "Anonymous", "ME" }, names);
        });
        Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-peers-pumbility']").ClassName);
        Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-scope-CompetitivePeers']").ClassName);
        Assert.Contains("PUMBILITY", dialog.Find("[data-testid='cld-peers-pumbility']").TextContent);

        // Switching to the other pool re-ranks off the competitive read; back again re-uses the kept ids.
        await dialog.Find("[data-testid='cld-peers-competitive']").ClickAsync(new MouseEventArgs());
        dialog.WaitForAssertion(() => Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-peers-competitive']").ClassName));
        await dialog.Find("[data-testid='cld-peers-pumbility']").ClickAsync(new MouseEventArgs());
        dialog.WaitForAssertion(() => Assert.Contains("cld-chip-on", dialog.Find("[data-testid='cld-peers-pumbility']").ClassName));
        _mediator.Verify(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     A peer the official board is the only record of has no account and no site score, so
    ///     they are read off the mirror by tag and stand on the board beside everyone else, wearing
    ///     its asterisk and its date (docs/design/pumbility-overhaul.md D59).
    /// </summary>
    [Fact]
    public void ABoardPeerStandsOnThePumbilityBoardWithTheMirrorsMark()
    {
        var me = Guid.NewGuid();
        SignedInAs(me);
        var peer = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ScoreFor(peer, 990_000, "PEER"), ScoreFor(me, 970_000, "ME") });
        _mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PeerVoice.Account(peer), PeerVoice.FromBoard(11, "URUSA#9487") });
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialTagScores(When,
                new[] { new OfficialTagScore("URUSA#9487", ChartId, 3, 995_000) }));

        var dialog = RenderDialog(MixEnum.Phoenix2, ChartLeaderboardScopes.LeaderboardScope.PumbilityPeers);

        dialog.WaitForAssertion(() =>
        {
            var names = dialog.FindAll(".weekly-lb-user").Select(e => e.TextContent.Trim()).ToArray();
            // The asterisk is the mirror's mark, the same one a ghost rival's row carries.
            Assert.Equal(new[] { "URUSA#9487*", "PEER", "ME" }, names);
        });
        Assert.Contains("Official board data", dialog.Markup);
    }

    [Fact]
    public void NoPumbilityPeersYetSaysWhatLightsThemUp()
    {
        SignedIn();
        var dialog = RenderDialog(MixEnum.Phoenix2, ChartLeaderboardScopes.LeaderboardScope.PumbilityPeers);

        dialog.WaitForAssertion(() => Assert.NotEmpty(dialog.FindAll("[data-testid='cld-empty']")));
        // The board reads the same sweep the PUMBILITY page does, so it names the same gate (D48).
        Assert.Contains($"a pool of {PeerGroup.PumbilityProjectionGate} charts",
            dialog.Find("[data-testid='cld-empty']").TextContent);
    }

    private void SignedIn() => SignedInAs(Guid.NewGuid());

    private void SignedInAs(Guid id)
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(id, Name.From("ME"), true, null,
            new Uri("https://example.invalid/me.png"), null));
    }

    private void GivenAFlaggedChart()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetLimboChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<Guid>)new HashSet<Guid> { ChartId });
    }

    [Fact]
    public void APassRanksAboveABrokenAttemptWhateverTheScore()
    {
        // A broken 990k used to outrank a passing 950k on the World board, which disagreed by a
        // row with the pass-only standing the popover prints for the same group (D21). The broken
        // row stays on the board, drawn with the broken grade, after every pass.
        var passer = Guid.NewGuid();
        var breaker = Guid.NewGuid();
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsForCommunityQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserPhoenixScore(breaker, ChartId, Name.From("BREAKER"), PhoenixScore.From(990_000), null, true,
                    true, When),
                ScoreFor(passer, 950_000, "PASSER")
            });

        var dialog = RenderDialog();

        dialog.WaitForAssertion(() =>
        {
            var rows = dialog.FindAll(".weekly-lb-row");
            Assert.Equal(2, rows.Count);
            Assert.Contains("PASSER", rows[0].TextContent);
            Assert.Contains("#1", rows[0].TextContent);
            Assert.Contains("BREAKER", rows[1].TextContent);
            Assert.Contains("#2", rows[1].TextContent);
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
            MixEnum.Phoenix, null, null);
    }
}
