using System.Linq;
using ScoreTracker.Translations.Contracts;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TranslationMarkersTests
{
    [Fact]
    public void AMarkerCarriesItsLevelAsDots()
    {
        Assert.Equal("⟦1⟧", TranslationMarkers.Marker(1, 0));
        Assert.Equal("⟦·2⟧", TranslationMarkers.Marker(2, 1));
        Assert.Equal("⟦··3⟧", TranslationMarkers.Marker(3, 2));
    }

    [Fact]
    public void PlainTextGetsLevelZero()
    {
        Assert.Equal(0, TranslationMarkers.PickLevel("check the drill at 2:01"));
    }

    [Fact]
    public void AnAuthorWhoTypesAMarkerPushesTheLevelPastTheirText()
    {
        var level = TranslationMarkers.PickLevel("I literally wrote ⟦1⟧ in my comment");

        Assert.True(level > 0);
        Assert.DoesNotContain(TranslationMarkers.Marker(1, level),
            "I literally wrote ⟦1⟧ in my comment");
    }

    [Fact]
    public void EscalatedAuthorTextEscalatesTheLevelAgain()
    {
        Assert.True(TranslationMarkers.PickLevel("weird ⟦·5⟧ flex") >= 2);
    }

    [Fact]
    public void MarkersAreListedInOrderOfAppearance()
    {
        Assert.Equal(new[] { "⟦2⟧", "⟦1⟧" }, TranslationMarkers.MarkersIn("b ⟦2⟧ a ⟦1⟧"));
    }

    [Fact]
    public void ACleanRoundTripHasNoViolationWhateverTheWordOrder()
    {
        Assert.Null(TranslationMarkers.Violation("proof: ⟦1⟧ and ⟦2⟧", "⟦2⟧ y también ⟦1⟧ como prueba"));
    }

    [Fact]
    public void ALostMarkerIsAViolation()
    {
        Assert.NotNull(TranslationMarkers.Violation("see ⟦1⟧ and ⟦2⟧", "mira ⟦1⟧"));
    }

    [Fact]
    public void AnInventedMarkerIsAViolation()
    {
        Assert.NotNull(TranslationMarkers.Violation("see ⟦1⟧", "mira ⟦1⟧ y ⟦2⟧"));
    }

    [Fact]
    public void ARepeatedMarkerIsAViolationEvenWhenTheCountLooksRight()
    {
        Assert.NotNull(TranslationMarkers.Violation("see ⟦1⟧ then ⟦2⟧", "mira ⟦1⟧ y ⟦1⟧"));
    }

    [Fact]
    public void LinkShapedTextGrowingInARenderingIsAViolation()
    {
        Assert.NotNull(TranslationMarkers.Violation("see ⟦1⟧", "mira ⟦1⟧ en https://evil.example"));
        Assert.NotNull(TranslationMarkers.Violation("no links here", "sin enlaces www.evil.example"));
    }

    [Fact]
    public void ASourceThatAlreadyReadsLikeALinkMayEchoIt()
    {
        // A bare host the caller's parser never lifted (no scheme) rides through as ordinary
        // text, and the rendering echoing it back is not an invention.
        Assert.Null(TranslationMarkers.Violation("try www.piugame.com maybe", "prueba www.piugame.com quizá"));
    }

    [Fact]
    public void SubstitutionRoundTripsThroughPickAndExtract()
    {
        var text = "watch ⟦1⟧ before you try";
        var markers = TranslationMarkers.MarkersIn(text);

        Assert.Single(markers);
        Assert.Equal("watch https://youtu.be/x before you try",
            text.Replace(markers.Single(), "https://youtu.be/x"));
    }
}
