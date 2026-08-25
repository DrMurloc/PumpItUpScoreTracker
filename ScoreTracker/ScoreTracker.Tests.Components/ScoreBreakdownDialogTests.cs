using Bunit;
using MudBlazor;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Score Breakdown Dialog's frame (D44): the play's identity, its counts, and the two
///     containers score-breakdown.js fills. The bars themselves are the calculator's shared
///     module and are not renderable under bUnit's loose JS runtime — what these facts pin is
///     everything the server side owns.
/// </summary>
public sealed class ScoreBreakdownDialogTests : ComponentTestBase
{
    private readonly Chart _chart = new(Guid.NewGuid(), MixEnum.Phoenix2,
        new Song("Sarabande", SongType.Arcade, new Uri("https://piu.test/art.png"),
            TimeSpan.FromMinutes(2), "MAX", Bpm.From(180, 180)),
        ChartType.Double, 23, MixEnum.Phoenix2, null, 900, new HashSet<Skill>());

    public ScoreBreakdownDialogTests()
    {
        this.RenderInteractive();
    }

    private IRenderedFragment RenderDialog(int? score, JudgementCounts? judgements,
        string? sourceLabel = null)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<ScoreBreakdownDialog>(1);
            builder.AddComponentParameter(2, nameof(ScoreBreakdownDialog.Visible), true);
            builder.AddComponentParameter(3, nameof(ScoreBreakdownDialog.Chart), _chart);
            builder.AddComponentParameter(4, nameof(ScoreBreakdownDialog.Mix), MixEnum.Phoenix2);
            builder.AddComponentParameter(5, nameof(ScoreBreakdownDialog.Score), score);
            builder.AddComponentParameter(6, nameof(ScoreBreakdownDialog.Judgements), judgements);
            builder.AddComponentParameter(7, nameof(ScoreBreakdownDialog.PlayedAt),
                (DateTimeOffset?)new DateTimeOffset(2026, 8, 23, 23, 41, 0, TimeSpan.Zero));
            builder.AddComponentParameter(8, nameof(ScoreBreakdownDialog.SourceLabel), sourceLabel);
            builder.CloseComponent();
        });
    }

    [Fact]
    public void AFilledPlayRendersItsIdentityCountsAndTheModuleContainers()
    {
        var cut = RenderDialog(923166, new JudgementCounts(731, 74, 11, 4, 18, 214), "Official Import");

        cut.WaitForAssertion(() =>
        {
            var dialog = cut.Find("[data-testid=score-breakdown-dialog]");
            Assert.Contains("Sarabande", dialog.TextContent);
            Assert.Contains("Official Import", dialog.TextContent);
            Assert.NotNull(cut.Find("[data-testid=judgement-strip]"));
            Assert.NotNull(cut.Find(".sbdlg-bars"));
            Assert.NotNull(cut.Find(".sbdlg-next"));
        });
    }

    [Fact]
    public void TheCalculatorLinkCarriesThePlaysCounts()
    {
        var cut = RenderDialog(923166, new JudgementCounts(731, 74, 11, 4, 18, 214));

        cut.WaitForAssertion(() =>
        {
            var link = cut.FindAll("a").Single(a => a.GetAttribute("href")!.Contains("perfects="));
            var href = link.GetAttribute("href")!;
            Assert.Contains("perfects=731", href);
            Assert.Contains("greats=74", href);
            Assert.Contains("goods=11", href);
            Assert.Contains("bads=4", href);
            Assert.Contains("misses=18", href);
            Assert.Contains("combo=214", href);
        });
    }

    [Fact]
    public void APlayWithoutAScoreRendersNothing()
    {
        // Belt to the caller's braces: stage breaks and score-less rows never open this, and
        // a dialog that reached here anyway shows an empty frame rather than a wrong story.
        var cut = RenderDialog(null, new JudgementCounts(500, 40, 9, 6, 22));

        cut.WaitForAssertion(() =>
            Assert.Empty(cut.FindAll("[data-testid=score-breakdown-dialog]")));
    }
}
