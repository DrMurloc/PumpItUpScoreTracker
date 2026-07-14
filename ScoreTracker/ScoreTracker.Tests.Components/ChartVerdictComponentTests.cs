using System;
using System.Collections.Generic;
using Bunit;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The facts-to-words boundary: the verdict engine ships typed facets, these pin the
///     sentences and chips they become. The pass-through localizer means assertions read
///     the English keys directly.
/// </summary>
public sealed class ChartVerdictComponentTests : ComponentTestBase
{
    [Fact]
    public void PassVsScoreSentenceComposesThePassAndScorePhrases()
    {
        var cut = RenderComponent<VerdictSentence>(p => p.Add(s => s.Facet,
            new PassVsScoreVerdict(TierListCategory.VeryHard, TierListCategory.Easy)));

        Assert.Contains("Very hard to pass for its level", cut.Markup);
        Assert.Contains("generous to score once you're through", cut.Markup);
    }

    [Fact]
    public void LetterWallSentenceNamesTheWallGrade()
    {
        var cut = RenderComponent<VerdictSentence>(p => p.Add(s => s.Facet,
            new LetterWallVerdict(ParagonLevel.SSS, 0.3)));

        Assert.Contains("Most clears stall below SSS", cut.Markup);
    }

    [Fact]
    public void PlateResidualSentenceFollowsTheSign()
    {
        var spiky = RenderComponent<VerdictSentence>(p => p.Add(s => s.Facet, new PlateResidualVerdict(-2)));
        var smooth = RenderComponent<VerdictSentence>(p => p.Add(s => s.Facet, new PlateResidualVerdict(2)));

        Assert.Contains("kill-spot signature", spiky.Markup);
        Assert.Contains("smooth attrition", smooth.Markup);
    }

    [Fact]
    public void PopulationSentenceCarriesTheCountAndRate()
    {
        var cut = RenderComponent<VerdictSentence>(p => p.Add(s => s.Facet, new PopulationVerdict(115, 71 / 115.0)));

        Assert.Contains("115", cut.Markup);
        Assert.Contains("62", cut.Markup); // 71/115 rounds to 62%
    }

    [Fact]
    public void VerdictCardLeadsWithTheFirstHeadlineFacetAndRendersTierChips()
    {
        IReadOnlyList<ChartVerdictFacet> facets = new ChartVerdictFacet[]
        {
            new PassVsScoreVerdict(TierListCategory.VeryHard, TierListCategory.Easy),
            new LetterWallVerdict(ParagonLevel.SSS, 0.3),
            new PlateResidualVerdict(-2),
            new PopulationVerdict(115, 0.62)
        };

        var cut = RenderComponent<ChartVerdictCard>(p => p.Add(c => c.Facets, facets));

        var head = cut.Find(".chart-verdict-head");
        Assert.Contains("Very hard to pass for its level", head.TextContent);
        var sub = cut.Find(".chart-verdict-sub");
        Assert.Contains("Most clears stall below SSS", sub.TextContent);
        Assert.Contains("Very Hard", cut.Markup); // pass tier chip
        Assert.Contains("Plates · spiky", cut.Markup);
    }

    [Fact]
    public void HistoryTimelineRendersDebutReratesAndDeltas()
    {
        var history = new HistoryVerdict(MixEnum.XX, new[]
        {
            new MixLevelRecord(MixEnum.XX, 19),
            new MixLevelRecord(MixEnum.Phoenix, 20),
            new MixLevelRecord(MixEnum.Phoenix2, 20)
        });

        var cut = RenderComponent<ChartHistoryTimeline>(p => p
            .Add(t => t.History, history)
            .Add(t => t.Type, ChartType.Double));

        Assert.Contains("debuted at D19", cut.Markup);
        Assert.Contains("rerated D20", cut.Markup);
        Assert.Contains("unchanged · D20", cut.Markup);
        Assert.Contains("+1", cut.Markup);
    }
}
