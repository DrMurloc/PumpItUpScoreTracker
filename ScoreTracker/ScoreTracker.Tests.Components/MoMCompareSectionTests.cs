using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     Compare (§11.3, D20): the session one place above yours is the default target, the four
///     numbers sit side by side with their deltas, the shared charts follow worst gap first, and a
///     past season shows the re-pricing ledger with its multiply note.
/// </summary>
public sealed class MoMCompareSectionTests : ComponentTestBase
{
    private readonly MoMSessionView _view = MoMComponentData.Session();
    private readonly List<CompareMoMSessionsQuery> _asked = new();

    public MoMCompareSectionTests()
    {
        Mediator.Setup(m => m.Send(It.IsAny<CompareMoMSessionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback((IRequest<MoMComparison?> q, CancellationToken _) => _asked.Add((CompareMoMSessionsQuery)q))
            .ReturnsAsync((IRequest<MoMComparison?> q, CancellationToken _) => Comparison(((CompareMoMSessionsQuery)q).OtherSessionId));
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private MoMComparison Comparison(Guid other)
    {
        var rival = _view.BoardSessions[1];
        var shared = new[]
        {
            new MoMSharedChart(_view.Charts[2].Chart.Chart, 844710, SharedKernel.Enums.PhoenixPlate.FairGame, false, 3207, 924890, SharedKernel.Enums.PhoenixPlate.FairGame, false, 5099),
            new MoMSharedChart(_view.Charts[0].Chart.Chart, 976489, SharedKernel.Enums.PhoenixPlate.FairGame, false, 1528, 983047, SharedKernel.Enums.PhoenixPlate.FairGame, false, 1622)
        };
        if (other == MoMComponentData.PastSessionId)
            return new MoMComparison(_view.SessionId, other, _view.Levers, rival.Levers, _view.Player, _view.OwnersPastSessions[0].Season,
                false, shared, new MoMRepricingSplit(4400, 4400, 801, 2447, 7648), false);
        return new MoMComparison(_view.SessionId, other, _view.Levers, rival.Levers, rival.Player, _view.Season, true, shared, null, false);
    }

    private IRenderedComponent<MoMCompareSection> Render(bool own = true) =>
        RenderComponent<MoMCompareSection>(p => p.Add(c => c.Session, _view).Add(c => c.IsOwn, own));

    [Fact]
    public void TheDefaultTargetIsTheSessionNextToYoursAndTheGridShowsTheDeltas()
    {
        var cut = Render();

        // First on the board, so the default target is the one below.
        Assert.Equal(MoMComponentData.RivalSessionId, Assert.Single(_asked).OtherSessionId);
        var cells = cut.FindAll(".mom-cmp-cell");
        Assert.Equal(4, cells.Count);
        Assert.Contains("3", cells[0].TextContent);
        Assert.Contains("vs 2", cells[0].TextContent);
        Assert.Contains("+1 charts", cells[0].TextContent);
        Assert.Contains("down", cells[3].QuerySelector(".d")!.ClassList); // more downtime than the rival reads as a loss
        var verdict = cut.Find("[data-testid=mom-verdict]").TextContent;
        Assert.Contains("1,388 ahead", verdict);
        Assert.Contains("1 more charts", verdict);
        Assert.Contains("lower average score", verdict);
        var rows = cut.FindAll("[data-testid=mom-shared-row]");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Gargoyle", rows[0].TextContent);
        Assert.Contains("−1,892", rows[0].QuerySelector(".mom-gap")!.TextContent);
        Assert.Contains("worst gap first", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=mom-ledger]"));
    }

    [Fact]
    public async Task APastSeasonShowsTheRepricingLedgerAndItsMultiplyNote()
    {
        var cut = Render();

        await cut.Find("[data-testid=mom-cmp-seasons]").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=mom-ledger]")));
        Assert.Contains(_asked, q => q.OtherSessionId == MoMComponentData.PastSessionId);
        var ledger = cut.Find("[data-testid=mom-ledger]").TextContent;
        Assert.Contains("March of Murlocs 2", ledger);
        Assert.Contains("4,400", ledger);
        Assert.Contains("+801", ledger);
        Assert.Contains("+2,447", ledger);
        Assert.Contains("7,648", ledger);
        Assert.Contains("priced as Winter 2025", ledger);
        Assert.Contains("The middle lines multiply, so they add to less than the total.", cut.Markup);
        Assert.Contains("Charts in both sessions", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=mom-verdict]"));
    }

    [Fact]
    public void AVisitorReadsTheirPastSeasons()
    {
        var cut = Render(own: false);
        Assert.Contains("Their past seasons", cut.Find("[data-testid=mom-cmp-seasons]").TextContent);
    }
}
