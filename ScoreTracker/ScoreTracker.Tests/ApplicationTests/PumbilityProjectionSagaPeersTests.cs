using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The Play page's read (docs/design/pumbility-overhaul.md §3.10) and the peer-ids read behind
///     the leaderboard chip, both off the same cached sweep the projection uses — Phoenix 2's
///     PUMBILITY band and, since D43, Phoenix 1's competitive band.
/// </summary>
public sealed partial class PumbilityProjectionSagaTests
{
    [Fact]
    public async Task ThePeersPageTiersWhatThePeersHoldAndLaysTheViewerOverIt()
    {
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_609.59)
            .WithChart(out var staple, ChartType.Single, 21)
            .WithChart(out var rare, ChartType.Single, 22)
            .WithChart(out var mine, ChartType.Single, 15);
        // Six peers all hold the staple; one of them also holds the rare chart. The viewer scored
        // the staple below every peer and holds a chart of their own that no peer does.
        var scores = new[] { 990_000, 985_000, 980_000, 975_000, 970_000, 965_000 };
        Guid last = Guid.Empty;
        foreach (var score in scores) ctx.WithPumbilityPeer(out last, staple, score);
        ctx.WithPeerPhoenix2Score(last, rare, 995_000);
        ctx.WithOwnScore(staple, 960_000).WithOwnScore(mine, 990_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix2, ChartType.Single),
            CancellationToken.None);

        Assert.True(page.Peers[ChartType.Single].IsLit);
        Assert.Equal(6, page.Peers[ChartType.Single].Size);
        var stapleEntry = Assert.Single(page.Entries, e => e.ChartId == staple.Id);
        Assert.Equal(6, stapleEntry.Holders);
        Assert.Equal(6, stapleEntry.PeerCount);
        Assert.Equal(6, stapleEntry.Scored);
        // The staple is every peer's #1 but the last's — the rare S22 outprices it there.
        Assert.Equal(50 * 5 + 49, stapleEntry.Points);
        Assert.Equal(977_500, (int)stapleEntry.Median!.Value);
        // The row's grade is the peers at the page's energy (D51, D52): Good is the first quartile,
        // which on six equal voices at 965k..990k lands exactly on the second.
        Assert.Equal(970_000, (int)stapleEntry.Projected!.Value);
        Assert.NotNull(stapleEntry.Variability);
        Assert.Equal(960_000, (int)stapleEntry.MyScore!.Value);
        Assert.Equal(0, stapleEntry.MyPercentile);
        Assert.Equal(1, stapleEntry.MyPoolRank); // the S21 outprices the S15 in the viewer's own pool

        var rareEntry = Assert.Single(page.Entries, e => e.ChartId == rare.Id);
        Assert.Equal(1, rareEntry.Holders);
        Assert.Equal(50, rareEntry.Points);
        Assert.Null(rareEntry.Median); // one scorer
        Assert.Null(rareEntry.Projected);
        Assert.Null(rareEntry.MyScore);
        // The tier enum runs best first, so the staple's tier sorts before the rare chart's.
        Assert.True((int)stapleEntry.Tier < (int)rareEntry.Tier);

        var alone = Assert.Single(page.YoursAlone);
        Assert.Equal(mine.Id, alone.ChartId);
        Assert.Equal(2, alone.MyPoolRank);
        Assert.Equal(990_000, (int)alone.Score);

        var compare = page.Compare[ChartType.Single];
        Assert.Equal(2, compare.MyLevels.Values.Sum());
        Assert.Equal(1, compare.PeerShareByLevel.Values.Sum(), 6);
    }

    [Fact]
    public async Task ThePeersPageReadsTheProjectedGradeAtTheEnergyAskedForOffOneSweep()
    {
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_609.59)
            .WithChart(out var staple, ChartType.Single, 21);
        foreach (var score in new[] { 990_000, 985_000, 980_000, 975_000, 970_000, 965_000 })
            ctx.WithPumbilityPeer(staple, score);

        var good = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix2, ChartType.Single),
            CancellationToken.None);
        var great = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix2, ChartType.Single,
            Energy.Great), CancellationToken.None);
        var top = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix2, ChartType.Single,
            Energy.TopOfMyGame), CancellationToken.None);

        Assert.Equal(970_000, (int)good.Entries.Single().Projected!.Value);
        Assert.Equal(977_500, (int)great.Entries.Single().Projected!.Value);
        Assert.Equal(985_000, (int)top.Entries.Single().Projected!.Value);
        // Three energies, one sweep: the band was drawn once.
        ctx.Stats.Verify(s => s.GetPlayersByPumbilityRange(MixEnum.Phoenix2, It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheRosterListsPublicPeersStrongestFirstCountsPrivateOnesAndPlacesTheViewer()
    {
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_609.59)
            .WithChart(out var chart, ChartType.Single, 21)
            .WithChart(out var mine, ChartType.Single, 15);
        ctx.WithPumbilityPeer(out var weaker, chart, 985_000, name: "Weaker", total: 17_100)
            .WithPumbilityPeer(out var stronger, chart, 990_000, name: "Stronger", total: 18_200)
            .WithPumbilityPeer(out _, chart, 980_000, name: "Hidden", isPublic: false, total: 17_800)
            .WithOwnScore(chart, 970_000).WithOwnScore(mine, 990_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix2, ChartType.Single),
            CancellationToken.None);

        Assert.Equal(new[] { "Stronger", "Weaker" }, page.Roster.Select(r => r.User.Name.ToString()).ToArray());
        Assert.Equal(1, page.PrivatePeers);
        Assert.Equal(3, page.Peers[ChartType.Single].Size); // the private peer is still a peer
        var top = page.Roster[0];
        Assert.Equal(stronger, top.User.Id);
        Assert.Equal(18_200, top.Total);
        Assert.Equal(new[] { ChartType.Single }, top.PeerFor.ToArray());
        // The peer's pool holds the shared chart, which sits in the viewer's own pool too.
        Assert.Equal(1, top.Overlap[ChartType.Single]);
        Assert.NotNull(page.You);
        Assert.Equal(ctx.UserId, page.You!.User.Id);
        Assert.Equal(17_609.59, page.You.Total);
        Assert.Empty(page.You.PeerFor);
    }

    [Fact]
    public async Task ADarkTypeOrAnotherMixAnswersEmptyButStillNamesTheGroup()
    {
        var dark = new ProjectionContext().WithPhoenix2Pool(29, 17_609.59)
            .WithChart(out var chart, ChartType.Single, 21);
        dark.WithPumbilityPeer(chart, 985_000);

        var page = await dark.Saga.Handle(new GetPumbilityPeersPageQuery(dark.UserId, MixEnum.Phoenix2, ChartType.Single),
            CancellationToken.None);
        Assert.Empty(page.Entries);
        Assert.Empty(page.Roster);
        Assert.False(page.Peers[ChartType.Single].IsLit);
        Assert.Equal(29, page.Peers[ChartType.Single].PoolCount);

    }

    [Fact]
    public async Task Phoenix1AnswersFromTheCompetitiveBandWithNoRungAndNoGate()
    {
        // D43: the band is the peer group. The viewer's pool is nowhere near fifty and it does not
        // matter; the peer's pool is two charts and they still cast their (short) vote.
        var ctx = new ProjectionContext()
            .WithChart(out var chart, ChartType.Single, 21)
            .WithChart(out var other, ChartType.Single, 20);
        ctx.WithPumbilityPeer(out var peer, chart, phoenix1Score: 975_000, name: "Rival", total: 1_234.5)
            .WithOwnScore(chart, 960_000);
        ctx.WithPeerPhoenix1Score(peer, other, 990_000);

        var page = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix),
            CancellationToken.None);

        Assert.Equal(PeerGroupKind.CompetitiveBand, page.Peers[ChartType.Single].Kind);
        Assert.True(page.Peers[ChartType.Single].IsLit);
        Assert.Equal(1, page.Peers[ChartType.Single].Size);
        var entry = Assert.Single(page.Entries, e => e.ChartId == chart.Id);
        Assert.Equal(1, entry.Holders);
        Assert.Equal(1, entry.MyPoolRank);
        Assert.Equal(960_000, (int)entry.MyScore!.Value);
        Assert.Contains(page.Entries, e => e.ChartId == other.Id && e.MyPoolRank == null);
        var row = Assert.Single(page.Roster);
        Assert.Equal("Rival", row.User.Name.ToString());
        Assert.Null(row.RungIndex);
        Assert.Equal(1_234.5, row.Total);
        Assert.Equal(1, row.Overlap[ChartType.Single]);
        Assert.Null(page.You!.RungIndex);
        ctx.Stats.Verify(s => s.GetPlayersByPumbilityRange(It.IsAny<MixEnum>(), It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Never);
        ctx.Stats.Verify(s => s.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Single, It.IsAny<double>(),
            It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThePeerIdsComeOffTheSameSweepAndAreEmptyWhereThereAreNoPeers()
    {
        var ctx = new ProjectionContext().WithPhoenix2Pool(50, 17_609.59)
            .WithChart(out var chart, ChartType.Single, 21);
        ctx.WithPumbilityPeer(out var a, chart, 985_000).WithPumbilityPeer(out var b, chart, 990_000)
            .WithPhoenix2DoublesPool(10);

        var singles = await ctx.Saga.Handle(new GetPumbilityPeersQuery(ChartType.Single, MixEnum.Phoenix2), CancellationToken.None);
        var doubles = await ctx.Saga.Handle(new GetPumbilityPeersQuery(ChartType.Double, MixEnum.Phoenix2), CancellationToken.None);
        var page = await ctx.Saga.Handle(new GetPumbilityPeersPageQuery(ctx.UserId, MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(new[] { a, b }.ToHashSet(), singles.ToHashSet());
        Assert.Empty(doubles); // the viewer's doubles pool is short: no doubles peers (D28)
        Assert.Contains(a, page.Roster.Select(r => r.User.Id));
        // One sweep served all three reads.
        ctx.Stats.Verify(s => s.GetPlayersByPumbilityRange(MixEnum.Phoenix2, It.IsAny<double>(), It.IsAny<double>(),
            It.IsAny<CancellationToken>()), Times.Once); // the one sweep; the dark doubles type never reaches the band read

        // On Phoenix 1 the same question answers the competitive band — these peers are in both.
        Assert.Equal(new[] { a, b }.ToHashSet(),
            (await ctx.Saga.Handle(new GetPumbilityPeersQuery(ChartType.Single, MixEnum.Phoenix), CancellationToken.None)).ToHashSet());

        ctx.CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);
        Assert.Empty(await ctx.Saga.Handle(new GetPumbilityPeersQuery(ChartType.Single, MixEnum.Phoenix2), CancellationToken.None));
        Assert.Empty(await ctx.Saga.Handle(new GetPumbilityPeersQuery(ChartType.Single, MixEnum.Phoenix), CancellationToken.None));
    }
}
