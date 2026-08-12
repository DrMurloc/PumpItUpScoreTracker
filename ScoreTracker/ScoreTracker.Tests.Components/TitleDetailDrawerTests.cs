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
}
