using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Theming;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The drawer's suggested-level block. <see cref="SuggestedTitleLevelTests" /> owns the
///     folder maths; these are the facts about what a player actually reads — that the grade
///     travels on the same line as its number, and that neither edge case renders as a hole.
/// </summary>
public sealed class TitleDetailDrawerTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();

    public TitleDetailDrawerTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleHoldersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0));
        Services.AddSingleton(_mediator.Object);
        // UserLabel property-injects the user repository for its hover card, and reads
        // RendererInfo — declared last, because SetRendererInfo freezes the service provider.
        Services.AddSingleton(Mock.Of<ScoreTracker.Domain.SecondaryPorts.IUserRepository>());
        this.RenderInteractive();
    }

    private IRenderedComponent<TitleDetailDrawer> Open(string titleName)
    {
        var progress = Phoenix2TitleList
            .BuildProgress(new Dictionary<Guid, Chart>(), Array.Empty<RecordedPhoenixScore>(),
                new HashSet<Name>())
            .Single(p => p.Title.Name == (Name)titleName);

        var rung = new TitleRung(progress, RungState.Locked, false, 0, 0, RarityBand.Common);

        return RenderComponent<TitleDetailDrawer>(p => p
            .Add(c => c.Rung, rung)
            .Add(c => c.Mix, MixEnum.Phoenix2)
            .Add(c => c.IsLoggedIn, true)
            .Add(c => c.TrackedPlayers, 1562));
    }

    private static string[] Rows(IRenderedComponent<TitleDetailDrawer> drawer)
    {
        return drawer.FindAll(".title-suggest-row")
            .Select(r => string.Join(" ",
                r.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .ToArray();
    }

    [Fact]
    public void EachGradeTravelsOnTheSameLineAsItsFolder()
    {
        // A grade in a shared caption underneath would let the numbers read as a progression:
        // the low number is the hard one, and only the row can say so.
        var rows = Rows(Open("[S] ADVANCED LV.1"));

        Assert.Equal(new[] { "S13 at SSS+", "S16 at AAA", "S20 at A" }, rows);
    }

    [Fact]
    public void AMergedPoolNamesBothTypesOnEveryRow()
    {
        var rows = Rows(Open("[P.B] GOLD"));

        Assert.Equal(new[] { "S13 · D14 at SSS+", "S16 · D17 at AAA", "S20 · D21 at A" }, rows);
    }

    [Fact]
    public void GradesSharingAFolderRenderOnceRatherThanAsIdenticalRows()
    {
        var rows = Rows(Open("[S] INTERMEDIATE LV.9"));

        Assert.Equal(new[] { "S10 at AAA or better", "S14 at A" }, rows);
    }

    [Fact]
    public void ATitleTheFloorFlattensRendersASingleRow()
    {
        var rows = Rows(Open("[S] INTERMEDIATE LV.1"));

        Assert.Equal("S10 at A or better", Assert.Single(rows));
    }

    [Fact]
    public void AGradeNoFolderReachesKeepsItsRowAndSaysWhy()
    {
        // The 20,000 capstone is the only title the top folder cannot reach at a bare A. Only
        // the last row is pinned here — the rows above it are another test's subject, and this
        // one is about the row that falls short still being shown, and saying so.
        var drawer = Open("ABYSS ABSOLUTE");

        Assert.Equal("S29 · D29 still isn't enough at A", Rows(drawer)[^1]);
        // Dimmed, because that number is the ceiling it falls short of rather than an answer.
        Assert.Single(drawer.FindAll(".title-suggest-row.short"));
    }

    [Fact]
    public void ThePlateIsStatedOnceBeneathTheRowsRatherThanOnEveryOne()
    {
        var drawer = Open("[S] ADVANCED LV.1");

        Assert.Contains("Fifty charts, TG plate.",
            drawer.FindAll(".title-hint").Select(h => h.TextContent.Trim()));
    }

    // ── The gem-rung level groups (docs/design/pumbility-levels.md §4) ──

    private static User Player(string name, Guid? id = null)
    {
        return new User(id ?? Guid.NewGuid(), name, true, name, new Uri("https://example.test/p.png"), "US");
    }

    private IRenderedComponent<TitleDetailDrawer> OpenWithHolders(string titleName,
        TitleHoldersRecord holders, Guid? currentUserId = null)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleHoldersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(holders);

        var progress = Phoenix2TitleList
            .BuildProgress(new Dictionary<Guid, Chart>(), Array.Empty<RecordedPhoenixScore>(),
                new HashSet<Name>())
            .Single(p => p.Title.Name == (Name)titleName);
        var rung = new TitleRung(progress, RungState.Locked, false, 0, 0, RarityBand.Common);

        return RenderComponent<TitleDetailDrawer>(p => p
            .Add(c => c.Rung, rung)
            .Add(c => c.Mix, MixEnum.Phoenix2)
            .Add(c => c.IsLoggedIn, true)
            .Add(c => c.CurrentUserId, currentUserId)
            .Add(c => c.TrackedPlayers, 1562));
    }

    [Fact]
    public void AGemRungGroupsItsHoldersByLevelStrongestFirst()
    {
        var drawer = OpenWithHolders("[P.B] DIAMOND", new TitleHoldersRecord(new[]
        {
            new TitleHolder(Player("Top"), 17_841.10),
            new TitleHolder(Player("Mid"), 17_641.20),
            new TitleHolder(Player("Low"), 17_050.90)
        }, 0));

        drawer.WaitForAssertion(() =>
        {
            var heads = drawer.FindAll(".title-lvl-head .title-lvl-name").Select(h => h.TextContent).ToArray();
            Assert.Equal(new[] { "LV.5", "LV.4", "LV.3", "LV.2", "LV.1" }, heads);

            // Membership: 17,841 stands on LV.5, 17,641 on LV.4, 17,050 on LV.1 — and the two
            // empty rungs in between stay drawn, because the gaps are part of the answer.
            var groups = drawer.FindAll(".title-lvl");
            Assert.Contains("Top", groups[0].TextContent);
            Assert.Contains("Mid", groups[1].TextContent);
            Assert.Contains("Low", groups[4].TextContent);
            Assert.Equal(2, drawer.FindAll(".title-lvl.empty").Count);
        });
    }

    [Fact]
    public void AHolderWhoOutranTheStandingSetClampsIntoTheGem()
    {
        // Stats already say ALEXANDRITE while the standing set still says DIAMOND — the holder
        // renders on this gem's top rung rather than vanishing into a level that isn't drawn.
        var drawer = OpenWithHolders("[P.B] DIAMOND", new TitleHoldersRecord(new[]
        {
            new TitleHolder(Player("Outran"), 19_050.00)
        }, 0));

        drawer.WaitForAssertion(() =>
        {
            var groups = drawer.FindAll(".title-lvl");
            Assert.Contains("Outran", groups[0].TextContent);
        });
    }

    [Fact]
    public void ThePoolPrintsWholeBesideEachName()
    {
        var drawer = OpenWithHolders("[P.B] DIAMOND", new TitleHoldersRecord(new[]
        {
            new TitleHolder(Player("Someone"), 17_641.20)
        }, 0));

        // N0 — decimals are a PUMBILITY-section feature, and this drawer is not that section.
        drawer.WaitForAssertion(() =>
            Assert.Equal("17,641", drawer.Find(".title-lvl-pool").TextContent));
    }

    [Fact]
    public void OnlyTheViewersOwnRowIsMarked()
    {
        var meId = Guid.NewGuid();
        var drawer = OpenWithHolders("[P.B] DIAMOND", new TitleHoldersRecord(new[]
        {
            new TitleHolder(Player("Me", meId), 17_641.20),
            new TitleHolder(Player("Neighbour"), 17_650.00)
        }, 0), meId);

        drawer.WaitForAssertion(() =>
        {
            // One marker in the whole list: the viewer's entry. The rung rows never highlight —
            // three marked things (the stand block, the rung, the entry) is a lot of highlighting.
            var mine = Assert.Single(drawer.FindAll(".olb-row-me"));
            Assert.Contains("Me", mine.TextContent);
            Assert.DoesNotContain("olb-row-me",
                string.Join(" ", drawer.FindAll(".title-lvl-head").Select(h => h.OuterHtml)));
        });
    }

    [Fact]
    public void AGemRungsProgressBarWearsTheRungsItClimbsThrough()
    {
        // The DIAMOND bar spans 16,000 → 17,000 — PLATINUM's four interior levels tick it.
        var drawer = OpenWithHolders("[P.B] DIAMOND", new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0));

        Assert.Equal(4, drawer.FindAll(".title-bar .pmb-lvl-tick").Count);
    }

    [Fact]
    public void TheFirstRungsBarStaysBareBecauseNothingSitsBelowBronze()
    {
        var drawer = OpenWithHolders("[P.B] BRONZE", new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0));

        Assert.Empty(drawer.FindAll(".title-bar .pmb-lvl-tick"));
    }

    [Fact]
    public void ALadderTitlesBarStaysBareOnThePerTypePools()
    {
        var drawer = OpenWithHolders("[S] ADVANCED LV.1", new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0));

        Assert.Empty(drawer.FindAll(".title-bar .pmb-lvl-tick"));
    }

    [Fact]
    public void ANonGemTitleKeepsItsFlatHolderList()
    {
        var drawer = OpenWithHolders("[S] ADVANCED LV.1", new TitleHoldersRecord(new[]
        {
            new TitleHolder(Player("Someone"))
        }, 0));

        drawer.WaitForAssertion(() =>
        {
            Assert.Single(drawer.FindAll(".title-holders"));
            Assert.Empty(drawer.FindAll(".title-lvls"));
        });
    }
}
