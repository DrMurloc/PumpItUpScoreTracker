using System;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class ProjectedBestTests
{
    private static readonly ScoringConfiguration Phoenix2 =
        ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);

    // Clematis Rapsodia S22: a single prices one level up the curve on Phoenix 2, at Base(23) = 245.
    private static readonly Chart S22 = new ChartBuilder()
        .WithMix(MixEnum.Phoenix2).WithType(ChartType.Single).WithLevel(22).Build();

    [Fact]
    public void AHigherScoreKeepsTheBetterPlateYouAlreadyHold()
    {
        // Held 985,708 Superb Game; the projected play is 991,632 Marvelous Game. The best card would
        // read 991,632 Superb Game: 245 × (1.49 + 0.008), not 245 × (1.49 + 0.006).
        var held = Held(985_708, PhoenixPlate.SuperbGame);

        var (score, plate) = ProjectedBest.After(held, 991_632, PhoenixPlate.MarvelousGame);

        Assert.Equal(991_632, (int)score);
        Assert.Equal(PhoenixPlate.SuperbGame, plate);
        Assert.Equal(367.01, ProjectedBest.Value(Phoenix2, S22, 991_632, PhoenixPlate.MarvelousGame, held), 2);
    }

    [Fact]
    public void ALowerScoreKeepsTheScoreYouAlreadyHoldAndTakesTheBetterPlate()
    {
        var held = Held(988_000, PhoenixPlate.MarvelousGame);

        var (score, plate) = ProjectedBest.After(held, 982_000, PhoenixPlate.SuperbGame);

        Assert.Equal(988_000, (int)score);
        Assert.Equal(PhoenixPlate.SuperbGame, plate);
    }

    [Fact]
    public void ABrokenRecordHasNoPlateToKeep()
    {
        // Any pass outranks a break, so the projection stands as it is — even under the broken score.
        var held = Held(995_000, null, true);

        var (score, plate) = ProjectedBest.After(held, 991_632, PhoenixPlate.MarvelousGame);

        Assert.Equal(991_632, (int)score);
        Assert.Equal(PhoenixPlate.MarvelousGame, plate);
    }

    [Fact]
    public void NothingHeldIsTheProjectionItself()
    {
        var (score, plate) = ProjectedBest.After(null, 991_632, PhoenixPlate.MarvelousGame);

        Assert.Equal(991_632, (int)score);
        Assert.Equal(PhoenixPlate.MarvelousGame, plate);
        Assert.Equal(Phoenix2.GetScore(S22, 991_632, PhoenixPlate.MarvelousGame, false),
            ProjectedBest.Value(Phoenix2, S22, 991_632, PhoenixPlate.MarvelousGame, null));
    }

    [Fact]
    public void AHeldPassWithNoPlateReadsAsRoughGame()
    {
        var held = Held(995_000, null);

        var (score, plate) = ProjectedBest.After(held, 991_632, PhoenixPlate.FairGame);

        Assert.Equal(995_000, (int)score);
        Assert.Equal(PhoenixPlate.FairGame, plate);
    }

    [Theory]
    [InlineData(980_000)]
    [InlineData(985_708)]
    public void APeerGuessAtOrBelowYourScoreBringsNoPlate(int projected)
    {
        // The curve guesses Marvelous Game at these scores, better than the Talented Game held. Read
        // off other players' scores that do not beat yours, it says nothing about your plate: at
        // your own score it would be a gain from the guess alone.
        var held = Held(985_708, PhoenixPlate.TalentedGame);

        var plate = ProjectedBest.PeerPlate(projected, held);

        Assert.Equal(PhoenixPlate.RoughGame, plate);
        Assert.Equal(Phoenix2.GetScore(S22, 985_708, PhoenixPlate.TalentedGame, false),
            ProjectedBest.Value(Phoenix2, S22, projected, plate, held));
    }

    [Fact]
    public void APeerGuessAboveYourScoreBringsTheCurvesPlate()
    {
        var held = Held(985_708, PhoenixPlate.SuperbGame);

        Assert.Equal(ScoringConfiguration.ExpectedPlateForScore(991_632),
            ProjectedBest.PeerPlate(991_632, held));
    }

    [Fact]
    public void ABrokenHoldDoesNotSilenceAPeerGuess()
    {
        var held = Held(995_000, null, true);

        Assert.Equal(ScoringConfiguration.ExpectedPlateForScore(991_632),
            ProjectedBest.PeerPlate(991_632, held));
        Assert.Equal(ScoringConfiguration.ExpectedPlateForScore(991_632),
            ProjectedBest.PeerPlate(991_632, null));
    }

    private static RecordedPhoenixScore Held(int score, PhoenixPlate? plate, bool isBroken = false)
    {
        return new RecordedPhoenixScore(S22.Id, score, plate, isBroken,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }
}
