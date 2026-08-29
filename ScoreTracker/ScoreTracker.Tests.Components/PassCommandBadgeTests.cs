using Bunit;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The badge is the sentence, so its src has to be right: these pin the file names against
///     piugame's own command-window art, including the plus a top grade keeps.
/// </summary>
public sealed class PassCommandBadgeTests : ComponentTestBase
{
    [Fact]
    public void APlateRendersTheCommandWindowsPlateArt()
    {
        var cut = RenderComponent<PassCommandBadge>(p => p.Add(c => c.Plate, PhoenixPlate.PerfectGame));

        var img = cut.Find("img.pass-command");
        Assert.Equal("https://piuimages.arroweclip.se/commands/Pass_Plate_PG.png",
            img.GetAttribute("src"));
    }

    [Fact]
    public void AGradeKeepsItsPlusInTheFileName()
    {
        var cut = RenderComponent<PassCommandBadge>(p => p.Add(c => c.Grade, PhoenixLetterGrade.SSSPlus));

        var img = cut.Find("img.pass-command");
        Assert.Equal("https://piuimages.arroweclip.se/commands/Pass_Grade_SSS+.png",
            img.GetAttribute("src"));
    }

    [Fact]
    public void ARunThatPutBothOutOfReachWearsBothBadges()
    {
        var cut = RenderComponent<PassCommandBadge>(p => p
            .Add(c => c.Plate, PhoenixPlate.UltimateGame)
            .Add(c => c.Grade, PhoenixLetterGrade.SSSPlus));

        var images = cut.FindAll("img.pass-command");
        Assert.Equal(2, images.Count);
        Assert.EndsWith("Pass_Plate_UG.png", images[0].GetAttribute("src"));
        Assert.EndsWith("Pass_Grade_SSS+.png", images[1].GetAttribute("src"));
    }

    [Fact]
    public void EveryBadgeCarriesAltTextNamingTheCommand()
    {
        // Alt text, not a title: the art is the only thing carrying the outcome on the row, so
        // it has to survive a screen reader and a broken image alike.
        var cut = RenderComponent<PassCommandBadge>(p => p.Add(c => c.Grade, PhoenixLetterGrade.SSS));

        Assert.Equal("Pass SSS ended this stage", cut.Find("img.pass-command").GetAttribute("alt"));
    }

    [Fact]
    public void NothingRendersWhenNeitherTargetIsKnown()
    {
        var cut = RenderComponent<PassCommandBadge>();

        Assert.Empty(cut.FindAll("img.pass-command"));
    }
}
