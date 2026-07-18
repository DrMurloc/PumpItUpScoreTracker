using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The shared board dialog after the challenges-hub overhaul: every entry renders (the
///     MaxPlaces cap died — M6), and rows wear the trust ladder (M5): ✔ imported, 📷 photo
///     proof opening the image, nothing for a bare self-report.
/// </summary>
public sealed class LeaderboardDialogTests : ComponentTestBase
{
    private readonly Chart _chart = new(Guid.NewGuid(), MixEnum.Phoenix,
        new Song("District 1", SongType.Arcade, new Uri("https://piu.test/art.png"),
            TimeSpan.FromMinutes(2), "Bang", Bpm.From(160, 160)),
        ChartType.Single, 20, MixEnum.Phoenix, null, 900, new HashSet<Skill>());

    public LeaderboardDialogTests()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => ids.Select(id =>
                new User(id, Name.From("P" + id.ToString("N")[..6]), true, null,
                    new Uri("https://piu.test/avatar.png"), null)).ToArray());
        Services.AddSingleton(users.Object);
        Services.AddSingleton(Mock.Of<IUiSettingsAccessor>());
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);
        // The dialog is an island; its rows render UserLabel/ScoreBreakdown on the live
        // (tooltip) path, which reads RendererInfo.
        this.RenderInteractive();
    }

    private IRenderedFragment RenderDialog(WeeklyTournamentEntry[] entries,
        IReadOnlySet<Guid>? officialIds = null, bool ascending = false, IReadOnlySet<Guid>? inRangeIds = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<LeaderboardDialog>(1);
            builder.AddComponentParameter(2, nameof(LeaderboardDialog.Visible), true);
            builder.AddComponentParameter(3, nameof(LeaderboardDialog.Chart), _chart);
            builder.AddComponentParameter(4, nameof(LeaderboardDialog.Entries), entries);
            builder.AddComponentParameter(5, nameof(LeaderboardDialog.OfficialUserIds), officialIds);
            builder.AddComponentParameter(6, nameof(LeaderboardDialog.Ascending), ascending);
            builder.AddComponentParameter(7, nameof(LeaderboardDialog.InRangeUserIds), inRangeIds);
            builder.CloseComponent();
        });
    }

    private WeeklyTournamentEntry Entry(int score, Uri? photo = null, bool isBroken = false)
    {
        return new WeeklyTournamentEntry(Guid.NewGuid(), _chart.Id, score, PhoenixPlate.SuperbGame,
            isBroken, photo, 18.0);
    }

    [Fact]
    public void EveryEntryRendersNoCapAnywhere()
    {
        // 14 entries — the pre-overhaul dialog would have stopped at ten.
        var entries = Enumerable.Range(0, 14).Select(i => Entry(990_000 - i * 1000)).ToArray();

        var cut = RenderDialog(entries);

        cut.WaitForAssertion(() =>
            Assert.Equal(14, cut.FindAll(".weekly-lb-row").Count));
    }

    [Fact]
    public void OfficialRowsWearTheVerifiedTagAndBareSelfReportsWearNothing()
    {
        var official = Entry(990_000);
        var bare = Entry(950_000);

        var cut = RenderDialog(new[] { official, bare }, officialIds: new HashSet<Guid> { official.UserId });

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll(".weekly-lb-verified"));
            Assert.Empty(cut.FindAll(".weekly-lb-photo"));
        });
    }

    [Fact]
    public void PhotoBackedSelfReportsLinkToTheirProof()
    {
        var photo = new Uri("https://images.example/proof.png");
        var entries = new[] { Entry(990_000, photo) };

        var cut = RenderDialog(entries, officialIds: new HashSet<Guid>());

        cut.WaitForAssertion(() =>
        {
            var link = cut.Find(".weekly-lb-photo");
            Assert.Equal(photo.ToString(), link.GetAttribute("href"));
            Assert.Equal("_blank", link.GetAttribute("target"));
        });
    }

    [Fact]
    public void TrustLegendShowsOnlyWhenTheBoardTracksProvenance()
    {
        var entries = new[] { Entry(990_000) };

        var withLadder = RenderDialog(entries, officialIds: new HashSet<Guid>());
        withLadder.WaitForAssertion(() => Assert.Single(withLadder.FindAll(".weekly-lb-legend")));

        var withoutLadder = RenderDialog(entries);
        withoutLadder.WaitForAssertion(() => Assert.Single(withoutLadder.FindAll(".weekly-lb-row")));
        Assert.Empty(withoutLadder.FindAll(".weekly-lb-legend"));
    }

    [Fact]
    public void AscendingBoardsRankPassingRunsLowestFirst()
    {
        var lowest = Entry(650_000);
        var broken = Entry(600_000, isBroken: true); // lower, but Limbo only ranks passes
        var higher = Entry(700_000);

        var cut = RenderDialog(new[] { higher, broken, lowest }, ascending: true);

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll(".weekly-lb-row");
            Assert.Equal(2, rows.Count);
            Assert.Contains("#1", rows[0].TextContent);
            Assert.Contains("650,000", rows[0].TextContent);
        });
    }

    [Fact]
    public void RelevantSwitchFiltersOutOfBandRowsAndRenumbers()
    {
        // Top score from out of band; two in-band rows behind it. The switch drops the
        // sandbagger and the survivors renumber from #1 (M20).
        var sandbagger = Entry(995_000);
        var second = Entry(970_000);
        var third = Entry(960_000);
        var inRange = new HashSet<Guid> { second.UserId, third.UserId };

        var cut = RenderDialog(new[] { sandbagger, second, third }, inRangeIds: inRange);

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".weekly-lb-row").Count));
        cut.Find(".challenge-switch input").Change(true);
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll(".weekly-lb-row");
            Assert.Equal(2, rows.Count);
            Assert.Contains("#1", rows[0].TextContent);
            Assert.Contains("970,000", rows[0].TextContent);
        });
    }

    [Fact]
    public void RelevantSwitchOnlyAppearsWhenTheBoardTracksTheBand()
    {
        var cut = RenderDialog(new[] { Entry(990_000) });

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".weekly-lb-row")));
        Assert.Empty(cut.FindAll(".challenge-switch"));
    }
}
