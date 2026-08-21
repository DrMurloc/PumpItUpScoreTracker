using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Application.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using ScoreTracker.Web.Services.Contracts;
using ScoreTracker.Web.Services.HomeDashboard;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Play block (docs/design/pumbility-overhaul.md §3.10): it reads the peers page off the
///     mediator for the frame's pool, opens on Prevalence with the gains switch on, shows the
///     Phoenix 1 switch only under Projected gains and only when carried rows exist, keeps the
///     density trio right above the list, swaps the gains to the peer-only projection when the
///     switch goes off — and on Phoenix 1 is the same block over the competitive band (D43).
/// </summary>
public sealed class PeersSectionTests : ComponentTestBase
{
    private static readonly Guid Viewer = Guid.NewGuid();
    private readonly Dictionary<Guid, Chart> _charts = new();

    public PeersSectionTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(Viewer, Name.From("Viewer"), true, Name.From("Viewer"),
            new Uri("https://piu.test/a.png"), Name.From("US")));
        // The roster glows crews and rivals through the shared reader, which the block resolves.
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>().AsEnumerable());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<RivalSubject>)Array.Empty<RivalSubject>());
        Services.AddScoped<CommunityGlowReader>();
        // The share card stamps a date; a fixed clock keeps the subtitle assertable.
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(c =>
            c.Now == new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void OpensOnPrevalenceWithTheGainsSwitchOnAndTheTrioAboveTheList()
    {
        var page = Page(carried: false);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, page).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        // The controls row: grouped-by, its switch, then the trio at the far end — and nothing in the head.
        Assert.Empty(cut.FindAll(".pmb-block-head .mud-button-group-root"));
        var controls = cut.Find("[data-testid=peers-controls]");
        Assert.NotNull(controls.QuerySelector(".pmb-peers-controls-end .mud-button-group-root"));
        // The switch is in the left group with the select, not adrift between it and the trio
        // (owner, field test rounds three–five): structure, not margins, decides where it sits.
        var start = controls.QuerySelector(".pmb-peers-controls-start")!;
        Assert.NotNull(start.QuerySelector("[data-testid=peers-groupby]"));
        Assert.NotNull(start.QuerySelector("[data-testid=peers-gains-switch]"));
        Assert.Null(start.QuerySelector(".mud-button-group-root"));
        Assert.Null(controls.QuerySelector("[data-testid=peers-p1-switch]"));
        // Prevalence + the gains cut: only the paying chart shows, in its tier.
        Assert.Contains("Staple", cut.Markup);
        Assert.Contains("Pays", cut.Markup);
        Assert.DoesNotContain("Free", cut.Markup);
        Mediator.Verify(m => m.Send(It.Is<GetPumbilityPeersPageQuery>(q => q.UserId == Viewer && q.Mix == MixEnum.Phoenix2 && q.Pool == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ThePhoenix1SwitchAppearsOnlyUnderProjectedGainsAndOnlyWithCarriedRows()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GroupBySettingKey)).ReturnsAsync(PeerGrouping.ProjectedGains.ToString());
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();

        var withCarried = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: true)).Add(x => x.Charts, _charts));
        withCarried.WaitForState(() => withCarried.FindAll("[data-testid=peers-controls]").Count > 0);
        Assert.NotNull(withCarried.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-p1-switch]"));
        Assert.Null(withCarried.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-gains-switch]"));
        // Under Projected gains the carried row interleaves by gain and the section is a band.
        Assert.Contains("+15 to +25", withCarried.Markup);
        Assert.Contains("Further", withCarried.Markup);

        var without = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts));
        without.WaitForState(() => without.FindAll("[data-testid=peers-controls]").Count > 0);
        Assert.Null(without.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-p1-switch]"));
    }

    [Fact]
    public async Task TurningThePhoenix1SwitchOffReadsThePeerOnlyProjectionAndDropsTheCarriedRows()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        var pays = _charts.Values.First(c => c.Song.Name == "Pays");
        Mediator.Setup(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityProjection(
                new Dictionary<Guid, PhoenixScore> { [pays.Id] = 987_475 },
                new Dictionary<Guid, double> { [pays.Id] = 12.5 },
                new Dictionary<Guid, TierListCategory>()));
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GroupBySettingKey)).ReturnsAsync(PeerGrouping.ProjectedGains.ToString());
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: true)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-p1-switch]").Count > 0);
        Assert.Contains("Further", cut.Markup);
        Assert.Contains("+18", cut.Markup);

        await cut.Find("[data-testid=peers-p1-switch]").ChangeAsync(new ChangeEventArgs { Value = false });
        cut.WaitForState(() => !cut.Markup.Contains("Further"));

        Assert.DoesNotContain("Further", cut.Markup);
        Assert.Contains("+12", cut.Markup); // the peer-only gain now prints on the paying chart
        Mediator.Verify(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        settings.Verify(s => s.SetSetting(PeersSection.ProjectPhoenix1SettingKey, "False"), Times.Once);
    }

    [Fact]
    public void TheCompareStripIsTheLevelBarsAndNothingElse()
    {
        var page = new PumbilityPeersPageRecord(MixEnum.Phoenix2, null,
            new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = PeerGroup.Pumbility(24, 23, 50) },
            Array.Empty<PeerPoolEntry>(), Array.Empty<PeerAloneEntry>(), Array.Empty<PeerRosterEntry>(), 0, null,
            new Dictionary<ChartType, PeerCompare>
            {
                [ChartType.Single] = new(
                    new Dictionary<int, int> { [20] = 16, [21] = 19, [26] = 1 },
                    new Dictionary<int, double> { [20] = .26, [21] = .26, [22] = .27 })
            });

        var cut = RenderComponent<PeerCompareStrip>(p => p.Add(x => x.Page, page));

        // The In-common and Yours-alone tiles are cut (owner, field test round two).
        Assert.Single(cut.FindAll(".pmb-compare-tile"));
        Assert.DoesNotContain("In common", cut.Markup);
        Assert.DoesNotContain("Yours alone", cut.Markup);
        // One axis for both rows, spanning every level either side reaches: 20 through 26.
        Assert.Equal(7, cut.FindAll(".pmb-levelaxis span").Count);
        Assert.Equal(7, cut.FindAll(".pmb-levelrow-you i").Count);
        Assert.Contains("You: 19 charts at 21", cut.Markup);
        Assert.Contains("Peers: 27% of their pool weight at 22", cut.Markup);
    }

    // ------------------------------------------------------------------ fixture

    private Chart NewChart(string name)
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            ChartType.Single, 21, MixEnum.Phoenix2, null, null, new HashSet<Skill>());
        _charts[chart.Id] = chart;
        return chart;
    }

    [Fact]
    public void OnPhoenix1TheSameBlockRendersOverTheCompetitiveBand()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers(MixEnum.Phoenix));

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false, MixEnum.Phoenix)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        Assert.Equal("PUMBILITY Targets", cut.Find(".pmb-block-title").TextContent.Trim());
        Assert.Contains("within one competitive level of you", cut.Find(".pmb-block-lede").TextContent);
        Assert.DoesNotContain("PUMBILITY levels", cut.Markup);
        // A competitive band is lit, so no chips line renders (field test round two).
        Assert.Empty(cut.FindAll(".pmb-peer-chip"));
        Assert.NotNull(cut.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-gains-switch]"));
        Assert.Contains("Staple", cut.Markup);
        Mediator.Verify(m => m.Send(It.Is<GetPumbilityPeersPageQuery>(q => q.UserId == Viewer && q.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheDownloadButtonRendersTheSectionsThroughTheSharedCard()
    {
        // Owner, field test round three: the tier list's own Download, on the tier list's own
        // card model — the sections and their ramp colours come off the rendered list.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        TierListShareCard? sent = null;
        Mediator.Setup(m => m.Send(It.IsAny<GetTierListShareCardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<byte[]> request, CancellationToken _) =>
            {
                sent = ((GetTierListShareCardQuery)request).Card;
                return new byte[] { 1, 2, 3 };
            });

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-download]").Count > 0);
        await cut.Find("[data-testid=peers-download]").ClickAsync(new MouseEventArgs());

        Assert.NotNull(sent);
        Assert.Equal("PUMBILITY Targets", sent!.Title);
        Assert.Contains("Prevalence", sent.Subtitle);
        var row = Assert.Single(sent.Rows);
        Assert.Equal("Staple", row.Name);
        var tile = Assert.Single(row.Tiles);
        Assert.Equal("https://piu.test/i.png", tile.JacketUrl);
        // A chart the fixture has no score on is neither passed nor To-Do: no border, no grade art.
        Assert.Null(tile.GradeUrl);
        Assert.Null(tile.BadgeHex);
        Assert.Equal(TileOutline.Dot, tile.Outline);
        // It pays, so the tile prints the same corner the Compact tile does (field test round four).
        Assert.Equal("+18", tile.CornerLabel); // PumbilityFormat: whole at 10 and up
    }

    [Fact]
    public async Task ADownloadedTileCarriesItsOwnDifficultyBubble()
    {
        // A tier list's card says its difficulty once, in the header, because the whole list is
        // one folder. This list is every difficulty at once, so the bubble has to ride the tile
        // or the picture cannot say what any of it is (owner, field test round six).
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        TierListShareCard? sent = null;
        Mediator.Setup(m => m.Send(It.IsAny<GetTierListShareCardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<byte[]> request, CancellationToken _) =>
            {
                sent = ((GetTierListShareCardQuery)request).Card;
                return new byte[] { 1 };
            });

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-download]").Count > 0);
        await cut.Find("[data-testid=peers-download]").ClickAsync(new MouseEventArgs());

        var tile = Assert.Single(Assert.Single(sent!.Rows).Tiles);
        // The fixture's charts are Phoenix 2 singles at 21 — the mix's own bubble art, the same
        // URL the page's card asks for.
        Assert.Equal("https://piuimages.arroweclip.se/difficulty/Phoenix2/s21.png", tile.BubbleUrl);
    }

    [Fact]
    public async Task ADownloadedTileWearsTheCompactCardsBorderAndItsGrade()
    {
        // Passed → solid green with the grade art; To-Do → dashed blue. The card is the grid.
        var pays = NewChart("Pays");
        var free = _charts.Values.FirstOrDefault(c => c.Song.Name == "Free") ?? NewChart("Free");
        var peers = new PumbilityPeersPageRecord(MixEnum.Phoenix2, null,
            new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = Group(MixEnum.Phoenix2) },
            new[]
            {
                new PeerPoolEntry(pays.Id, ChartType.Single, 12, 23, 500, TierListCategory.Overrated, 0, 12,
                    987_475, 980_000, 991_000, null, 3, 966_887, PhoenixPlate.MarvelousGame, null),
                new PeerPoolEntry(free.Id, ChartType.Single, 10, 23, 450, TierListCategory.Overrated, 1, 10,
                    null, null, null, null, null, null, null, null)
            },
            Array.Empty<PeerAloneEntry>(), Array.Empty<PeerRosterEntry>(), 0, null, new Dictionary<ChartType, PeerCompare>());
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(peers);
        TierListShareCard? sent = null;
        Mediator.Setup(m => m.Send(It.IsAny<GetTierListShareCardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<byte[]> request, CancellationToken _) =>
            {
                sent = ((GetTierListShareCardQuery)request).Card;
                return new byte[] { 1 };
            });
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GainsOnlySettingKey)).ReturnsAsync(false.ToString());
        Services.AddSingleton(settings.Object);

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts)
            .Add(x => x.ToDos, (ISet<Guid>)new HashSet<Guid> { free.Id }));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-download]").Count > 0);
        await cut.Find("[data-testid=peers-download]").ClickAsync(new MouseEventArgs());

        var tiles = Assert.Single(sent!.Rows).Tiles;
        var passed = tiles.First(t => t.GradeUrl != null);
        Assert.Equal(TileOutline.Solid, passed.Outline);
        Assert.Equal(MixPalette.Success, passed.BadgeHex);
        Assert.Contains("letters/aa", passed.GradeUrl);
        // The corner value takes the plate's place, exactly as it does on the Compact tile.
        Assert.Null(passed.PlateUrl);
        var todo = tiles.First(t => t.GradeUrl == null);
        Assert.Equal(TileOutline.Dashed, todo.Outline);
        Assert.Equal(MixPalette.Info, todo.BadgeHex);
    }

    [Fact]
    public void YourTop50IsTheThirdOptionAndCarriesNoSwitches()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GroupBySettingKey)).ReturnsAsync(PeerGrouping.YourTop50.ToString());
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: true)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        var controls = cut.Find("[data-testid=peers-controls]");
        Assert.Null(controls.QuerySelector("[data-testid=peers-p1-switch]"));
        Assert.Null(controls.QuerySelector("[data-testid=peers-gains-switch]"));
        Assert.NotNull(controls.QuerySelector(".pmb-peers-controls-end .mud-button-group-root"));
        Assert.Contains("Your pool by place", cut.Find(".pmb-block-lede").TextContent);
        // The frame's pool in this fixture is empty, and the lens says so rather than showing tiers.
        Assert.Contains("Nothing in your pool yet", cut.Find("[data-testid=ppl-empty]").TextContent);
        Assert.DoesNotContain("Staple", cut.Markup);
    }

    [Fact]
    public void ARunThatFellBelowTheFivePeerFloorSaysSoUnderTheLede()
    {
        // A two-peer band can never put five voices on a chart, so every row below is running on
        // the D47 fallback. The note says it once, under the lede — not a chip (owner).
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false,
                group: PeerGroup.Pumbility(24, 2, 50) with { AnsweredBelowFloor = true }))
            .Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        var note = cut.Find("[data-testid=peers-thin-note]");
        Assert.Contains("Players at your level with a full pool: 2", note.TextContent);
        // It belongs to the lede's own slot, not the chips line.
        Assert.Empty(cut.FindAll(".pmb-peer-line [data-testid=peers-thin-note]"));
    }

    [Fact]
    public void ABigBandWhoseChartsWereThinSaysSoToo()
    {
        // The case the old size test missed. Twenty-three peers is far over the floor, so nothing
        // about the BAND is thin — but if no chart of theirs collected five of them the run still
        // relaxed, and every row on screen rests on fewer than five scores. The projector says
        // whether that happened; the size of the band cannot.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false,
                group: PeerGroup.Pumbility(24, 23, 50) with { AnsweredBelowFloor = true }))
            .Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        Assert.Contains("Players at your level with a full pool: 23",
            cut.Find("[data-testid=peers-thin-note]").TextContent);
    }

    [Fact]
    public void ASmallBandThatStillMetTheFloorSaysNothing()
    {
        // The mirror of the above: a three-peer band whose one chart all three plus two others
        // scored answered at full strength. The note would be describing evidence that is there.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false, group: PeerGroup.Pumbility(24, 3, 50)))
            .Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        Assert.Empty(cut.FindAll("[data-testid=peers-thin-note]"));
    }

    [Fact]
    public void AHealthyBandAndPhoenix1SayNothingAboutTheFloor()
    {
        // Phoenix 1 has no five-peer floor at all — a thin competitive band simply casts a
        // shorter vote (D43), so the note would be describing a rule that does not apply there.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var healthy = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts));
        healthy.WaitForState(() => healthy.FindAll("[data-testid=peers-controls]").Count > 0);
        Assert.Empty(healthy.FindAll("[data-testid=peers-thin-note]"));

        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers(MixEnum.Phoenix));
        var phoenix1 = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false, MixEnum.Phoenix, PeerGroup.Competitive(21.4, 1.0, 2)))
            .Add(x => x.Charts, _charts));
        phoenix1.WaitForState(() => phoenix1.FindAll("[data-testid=peers-controls]").Count > 0);
        Assert.Empty(phoenix1.FindAll("[data-testid=peers-thin-note]"));
    }

    [Fact]
    public void AShortPoolSaysWhichFinishItsPeersWereDrawnFrom()
    {
        // Lit on twenty charts, so the band was drawn from where this player would finish rather
        // than from the thirty-one they hold (D48) — the note names both, and the gem comes off
        // the group's own centre so the page cannot name a rung the projection did not use.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p
            // Rung 13 is GOLD LV.3; the viewer holds 31 charts against a gate of 20.
            .Add(x => x.Page, Page(carried: false,
                group: PeerGroup.Pumbility(13, 23, 31, 20, placedByEstimate: true)))
            .Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        var note = cut.Find("[data-testid=peers-short-pool-note]").TextContent;
        Assert.Contains("You have 31 of 50 charts", note);
        Assert.Contains("[P.B] GOLD", note);
    }

    [Fact]
    public void AFullPoolSaysNothingAboutAProjectedFinish()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        Assert.Empty(cut.FindAll("[data-testid=peers-short-pool-note]"));
    }

    [Fact]
    public void AShortTypePoolOnASettledTotalIsNotAProjection()
    {
        // A full merged fifty and twenty-nine doubles: the band lights on the shorter gate, but
        // the rung came off a real number. The pool count alone would read this as a projection
        // and tell the player their peers came from an estimate that never happened.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page(carried: false, group: PeerGroup.Pumbility(24, 23, 29, 20)))
            .Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        Assert.Empty(cut.FindAll("[data-testid=peers-short-pool-note]"));
    }

    private static PeerGroup Group(MixEnum mix) =>
        mix == MixEnum.Phoenix2 ? PeerGroup.Pumbility(24, 23, 50) : PeerGroup.Competitive(21.4, 1.0, 144);

    /// <summary>A page record whose target list holds one paying peer row and, optionally, one carried row.</summary>
    private PumbilityPageRecord Page(bool carried, MixEnum mix = MixEnum.Phoenix2, PeerGroup? group = null)
    {
        var pays = _charts.Values.FirstOrDefault(c => c.Song.Name == "Pays") ?? NewChart("Pays");
        _ = _charts.Values.FirstOrDefault(c => c.Song.Name == "Free") ?? NewChart("Free");
        var targets = new List<PumbilityTarget> { new(pays.Id, 987_475, 18.4, null, false, null) };
        if (carried)
        {
            var further = _charts.Values.FirstOrDefault(c => c.Song.Name == "Further") ?? NewChart("Further");
            targets.Add(new PumbilityTarget(further.Id, 980_127, 24.4, null, false, null, TargetSource.Phoenix1));
        }

        return new PumbilityPageRecord(mix, null, 17_609.59, 345.94, null, Array.Empty<PoolEntry>(),
            Array.Empty<PoolEntry>(), targets,
            Peers: new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = group ?? Group(mix) });
    }

    private PumbilityPeersPageRecord Peers(MixEnum mix = MixEnum.Phoenix2)
    {
        var pays = _charts.Values.FirstOrDefault(c => c.Song.Name == "Pays") ?? NewChart("Pays");
        var free = _charts.Values.FirstOrDefault(c => c.Song.Name == "Free") ?? NewChart("Free");
        return new PumbilityPeersPageRecord(mix, null,
            new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = Group(mix) },
            new[]
            {
                new PeerPoolEntry(pays.Id, ChartType.Single, 12, 23, 500, TierListCategory.Overrated, 0, 12, 987_475, 980_000, 991_000, null, null, null, null, null),
                new PeerPoolEntry(free.Id, ChartType.Single, 10, 23, 450, TierListCategory.Overrated, 1, 10, null, null, null, null, null, null, null, null)
            },
            Array.Empty<PeerAloneEntry>(), Array.Empty<PeerRosterEntry>(), 0, null, new Dictionary<ChartType, PeerCompare>());
    }
}
