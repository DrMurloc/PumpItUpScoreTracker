using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class PatienceCardTests : ComponentTestBase
{
    private readonly Mock<IRandomNumberGenerator> _random = new();

    // Registered here rather than per render: bUnit seals its service provider the moment a
    // component asks it for anything, so a second registration after the first render throws.
    public PatienceCardTests()
    {
        Services.AddSingleton(_random.Object);
    }

    [Fact]
    public void TheCardSaysWhatIsHappeningAndKeepsTheFlavourSeparate()
    {
        // Two lines with two owners: the page supplies the honest one, so it is always
        // specific, and the card supplies the flavour, so it can never be wrong somewhere it
        // was not written for.
        var cut = Render("Working out your projections.");

        Assert.Equal("Working out your projections.", cut.Find(".patience-sub").TextContent.Trim());
        Assert.False(string.IsNullOrWhiteSpace(cut.Find(".patience-line").TextContent));
    }

    [Fact]
    public void ThePadHasFivePanelsBecauseThatIsWhatAPadHas()
    {
        var cut = Render("Anything.");

        Assert.Equal(5, cut.FindAll(".patience-panel").Count);
    }

    [Fact]
    public void ThePhraseComesFromTheSeamRatherThanFromAmbientRandomness()
    {
        // IRandomNumberGenerator is the seam every other caller uses, so the phrase is
        // pinnable and nothing here reaches for Random.Shared.
        _random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
        var first = Render("Anything.").Find(".patience-line").TextContent;

        _random.Setup(r => r.Next(It.IsAny<int>())).Returns(1);
        var second = Render("Anything.").Find(".patience-line").TextContent;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ThePadRunsOneOfTheRealStepPatterns()
    {
        // The pad steps a chart, not a decorative sweep, so the pattern has to reach the markup
        // — the schedule itself lives in CSS.
        _random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
        var pad = Render("Anything.").Find(".patience-pad");

        Assert.Contains("patience-mrun", pad.ClassList);
    }

    [Fact]
    public void ThePatternComesFromTheSameSeamAsThePhrase()
    {
        _random.Setup(r => r.Next(It.IsAny<int>())).Returns(0);
        var first = Render("Anything.").Find(".patience-pad").ClassName;

        _random.Setup(r => r.Next(It.IsAny<int>())).Returns(2);
        var second = Render("Anything.").Find(".patience-pad").ClassName;

        Assert.NotEqual(first, second);
    }

    private IRenderedComponent<PatienceCard> Render(string explanation)
    {
        return RenderComponent<PatienceCard>(p => p.Add(x => x.Explanation, explanation));
    }
}
