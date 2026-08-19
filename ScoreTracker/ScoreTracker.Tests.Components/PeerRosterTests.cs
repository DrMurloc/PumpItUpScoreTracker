using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The peers' roster (docs/design/pumbility-overhaul.md D39) and the variability meter (D35).
/// </summary>
public sealed class PeerRosterTests : ComponentTestBase
{
    // UserLabel resolves the user repository for its flag lookup; a loose stub keeps it renderable.
    public PeerRosterTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        this.RenderInteractive();
    }

    [Fact]
    public void TheRosterListsPeersStrongestFirstPlacesTheViewerAndCountsThePrivateOnes()
    {
        var rows = new[]
        {
            Row("Weaker", 17_100.25, 21, singles: 20.5, doubles: 19.0, overlap: 5),
            Row("Stronger", 18_387.09, 27, singles: 22.53, doubles: 24.52, overlap: 3)
        };
        var you = Row("Viewer", 17_609.59, 24, singles: 21.4, doubles: 21.1, overlap: 0);

        var cut = RenderComponent<PeerRoster>(p => p.Add(x => x.Rows, rows).Add(x => x.You, you)
            .Add(x => x.PrivatePeers, 7));

        var names = cut.FindAll("tbody tr").Select(r => r.QuerySelector("td:nth-child(2)")!.TextContent.Trim()).ToArray();
        Assert.Equal("Stronger", names[0].Split(' ')[0]);
        Assert.StartsWith("Viewer", names[1]);
        Assert.Equal("Weaker", names[2].Split(' ')[0]);
        // The viewer's row is highlighted, unnumbered, and does not shift the peers' places.
        var yours = cut.Find("[data-testid=roster-you]");
        Assert.Contains("You", yours.TextContent);
        Assert.Equal(string.Empty, yours.QuerySelector("td")!.TextContent.Trim());
        var places = cut.FindAll("[data-testid=roster-peer] td:first-child").Select(c => c.TextContent.Trim()).ToArray();
        Assert.Equal(new[] { "1", "2" }, places);
        // Totals print at two decimals — a pool total in the PUMBILITY section — and levels are named.
        Assert.Contains("18,387.09", cut.Markup);
        Assert.Contains("RED BERYL LV.2", cut.Markup);
        Assert.Contains("22.53", cut.Markup);
        Assert.Contains("7 private accounts are peers but are not shown.", cut.Find("[data-testid=roster-private]").TextContent);
    }

    [Fact]
    public void ThePeerForColumnAppearsOnlyWhenAskedAndSaysWhichTypes()
    {
        var both = Row("Both", 18_000, 26, 22, 22, 3, ChartType.Single, ChartType.Double);
        var singles = Row("Singles", 17_500, 23, 21, 20, 2, ChartType.Single);

        var withColumn = RenderComponent<PeerRoster>(p => p.Add(x => x.Rows, new[] { both, singles }).Add(x => x.ShowPeerFor, true));
        Assert.Contains("Peer for", withColumn.Find("thead").TextContent);
        Assert.Contains("Both", withColumn.FindAll("tbody tr")[0].TextContent);
        Assert.Contains("Singles", withColumn.FindAll("tbody tr")[1].TextContent);

        var without = RenderComponent<PeerRoster>(p => p.Add(x => x.Rows, new[] { both, singles }));
        Assert.DoesNotContain("Peer for", without.Find("thead").TextContent);
    }

    [Fact]
    public void TheMeterLightsOneDotPerLevelAndAlwaysPrintsTheWord()
    {
        var cut = RenderComponent<VariabilityMeter>(p => p.Add(x => x.Level, PeerVariabilityLevel.VerySplit).Add(x => x.Title, "From 12 peers"));
        Assert.Equal(5, cut.FindAll(".pmb-vary-dots i.on").Count);
        Assert.Equal("Very split", cut.Find(".pmb-vary-word").TextContent);
        Assert.Contains("--vary-5", cut.Find(".pmb-vary").GetAttribute("style"));
        Assert.Equal("From 12 peers", cut.Find(".pmb-vary").GetAttribute("title"));

        var calm = RenderComponent<VariabilityMeter>(p => p.Add(x => x.Level, PeerVariabilityLevel.VeryConsistent));
        Assert.Single(calm.FindAll(".pmb-vary-dots i.on"));
        Assert.Equal("Very consistent", calm.Find(".pmb-vary-word").TextContent);
    }

    [Fact]
    public void ABandSizedRosterKeepsTheFiftyAroundYouAndCountsTheRest()
    {
        // D43: a competitive band is several hundred players. The window is the fifty nearest the
        // viewer in the sort, the viewer in place, places unbroken, and the rest counted either side.
        var rows = Enumerable.Range(0, 120).Select(i => Row($"Peer {i}", 20_000 - i * 10, null, 21, 21, 2)).ToArray();
        var you = Row("Viewer", 20_000 - 80 * 10 + 5, null, 21, 21, 0); // 80 peers above, 40 below

        var cut = RenderComponent<PeerRoster>(p => p.Add(x => x.Rows, rows).Add(x => x.You, you));

        Assert.Equal(PeerRoster.Window, cut.FindAll("tbody tr").Count);
        Assert.Single(cut.FindAll("[data-testid=roster-you]"));
        Assert.Equal("55 more peers above", cut.Find("[data-testid=roster-more-above]").TextContent.Trim());
        Assert.Equal("16 more peers below", cut.Find("[data-testid=roster-more-below]").TextContent.Trim());
        var places = cut.FindAll("[data-testid=roster-peer] td:first-child").Select(c => int.Parse(c.TextContent.Trim())).ToArray();
        Assert.Equal(56, places[0]);
        Assert.Equal(Enumerable.Range(56, 49), places);
        // No ladder on the mix: no gem column.
        Assert.DoesNotContain(cut.FindAll("thead th"), th => th.TextContent.Trim() == "Level");
    }

    [Fact]
    public void FiftyOrFewerRowsIsTheWholeRosterWithNoWindowNotes()
    {
        var rows = Enumerable.Range(0, 49).Select(i => Row($"Peer {i}", 20_000 - i * 10, 24, 21, 21, 2)).ToArray();
        var you = Row("Viewer", 19_755, 24, 21, 21, 0);

        var cut = RenderComponent<PeerRoster>(p => p.Add(x => x.Rows, rows).Add(x => x.You, you));

        Assert.Equal(50, cut.FindAll("tbody tr").Count);
        Assert.Empty(cut.FindAll("[data-testid=roster-more-above]"));
        Assert.Empty(cut.FindAll("[data-testid=roster-more-below]"));
        Assert.Contains(cut.FindAll("thead th"), th => th.TextContent.Trim() == "Level");
    }

    [Fact]
    public void ACrewmateGlowsGreenARivalRedAndBothCarriesBothEnds()
    {
        // Owner, field test round one: the site's own row vocabulary, through the shared ladder.
        var rival = Row("Rival", 18_000, 24, 21, 21, 3);
        var crew = Row("Crew", 17_800, 24, 21, 21, 3);
        var both = Row("Both", 17_600, 24, 21, 21, 3);
        var stranger = Row("Stranger", 17_400, 24, 21, 21, 3);
        var you = Row("Viewer", 17_500, 24, 21, 21, 0);

        var cut = RenderComponent<PeerRoster>(p => p
            .Add(x => x.Rows, new[] { rival, crew, both, stranger }).Add(x => x.You, you)
            .Add(x => x.Rivals, (IReadOnlySet<Guid>)new HashSet<Guid> { rival.User.Id, both.User.Id })
            .Add(x => x.Clubmates, (IReadOnlySet<Guid>)new HashSet<Guid> { crew.User.Id, both.User.Id }));

        string ClassOf(string name) => cut.FindAll("tbody tr")
            .First(r => r.QuerySelector("td:nth-child(2)")!.TextContent.Contains(name)).ClassName ?? string.Empty;
        Assert.Equal("is-rival", ClassOf("Rival"));
        Assert.Equal("is-community", ClassOf("Crew"));
        Assert.Equal("is-both", ClassOf("Both"));
        Assert.Equal(string.Empty, ClassOf("Stranger"));
        // You win the ladder outright — a viewer who is somehow also in the sets stays your colour.
        Assert.Equal("pmb-roster-you", ClassOf("Viewer"));
    }

    private static PeerRosterEntry Row(string name, double total, int? rung, double singles, double doubles, int overlap,
        params ChartType[] peerFor)
    {
        var types = peerFor.Length == 0 ? new[] { ChartType.Single } : peerFor;
        return new PeerRosterEntry(
            new User(Guid.NewGuid(), Name.From(name), true, Name.From(name), new Uri("https://piu.test/a.png"), Name.From("US")),
            total, rung, singles, doubles, types.ToHashSet(),
            types.ToDictionary(t => t, _ => overlap));
    }
}
