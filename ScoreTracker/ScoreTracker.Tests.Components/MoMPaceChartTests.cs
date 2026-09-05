using System;
using System.Linq;
using Bunit;
using Microsoft.AspNetCore.Components;
using ScoreTracker.Web.Components.MoM;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>The pace chart: one dot and one shaded span per chart, the average as a dashed line, labels in the reader's words.</summary>
public sealed class MoMPaceChartTests : ComponentTestBase
{
    public MoMPaceChartTests() => SetRendererInfo(new RendererInfo("Server", true));

    [Fact]
    public void OneDotAndOneSpanPerChartAndTheAverageLine()
    {
        var view = MoMComponentData.Session();

        var cut = RenderComponent<MoMPaceChart>(p => p
            .Add(c => c.Charts, view.Charts)
            .Add(c => c.Window, view.Window));

        var svg = cut.Find("svg.mom-pace");
        Assert.Equal("Points per second across the session", svg.GetAttribute("aria-label"));
        Assert.Equal(3, svg.QuerySelectorAll("circle.dot").Length);
        Assert.Equal(3, svg.QuerySelectorAll("rect.rest").Length);
        Assert.Single(svg.QuerySelectorAll("line.avg"));
        Assert.Contains("avg 10.7", cut.Markup); // 6,388 points over 596 seconds of song
        Assert.Contains("Gargoyle - FULL SONG -", cut.Markup);
        // Culture-proof coordinates: a dot for the decimal point, never a comma.
        Assert.DoesNotContain(",\"", cut.Find("polyline").GetAttribute("points"));
        Assert.Contains("105m", cut.Markup);
    }

    [Fact]
    public void AnEmptySessionStillDrawsTheFrame()
    {
        var cut = RenderComponent<MoMPaceChart>(p => p
            .Add(c => c.Charts, Array.Empty<ScoreTracker.EventCompetition.Contracts.MoMTimedChart>())
            .Add(c => c.Window, TimeSpan.FromMinutes(105)));

        Assert.NotEmpty(cut.FindAll("svg.mom-pace line.grid"));
        Assert.Empty(cut.FindAll("circle.dot"));
    }
}
