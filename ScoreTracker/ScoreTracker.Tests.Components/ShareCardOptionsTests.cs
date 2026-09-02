using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The remembered download choices: today-parity defaults, a round-trip that survives
///     "everything off", and tolerance for tokens a newer release wrote.
/// </summary>
public sealed class ShareCardOptionsTests
{
    [Fact]
    public void DefaultsAreTodayParity()
    {
        var options = ShareCardOptions.Default;

        Assert.True(options.LetterGrades);
        Assert.True(options.Plates);
        Assert.True(options.BoundaryPass);
        Assert.True(options.BoundaryTodo);
        Assert.True(options.BoundaryBroken);
        Assert.True(options.IncludeBrokenScores);
        Assert.False(options.SongNames);
        Assert.False(options.Scores);
        Assert.False(options.Pumbility);
        Assert.False(options.ExpectedGains);
        Assert.False(options.Skills);
        Assert.False(options.ColorByLetterGrade);
        Assert.False(options.ColorByPlate);
        Assert.False(options.BoundaryOtherMixes);
        Assert.False(options.BoundaryTop50);
    }

    [Fact]
    public void ANeverSavedSettingParsesToTheDefaults()
    {
        Assert.Equal(ShareCardOptions.Default, ShareCardOptions.Parse(null));
        Assert.Equal(ShareCardOptions.Default, ShareCardOptions.Parse("  "));
    }

    [Fact]
    public void EverythingOffRoundTripsAsEverythingOffNotAsTheDefaults()
    {
        var allOff = ShareCardOptions.Default with
        {
            LetterGrades = false, Plates = false, IncludeBrokenScores = false,
            BoundaryTodo = false, BoundaryPass = false, BoundaryBroken = false
        };

        var parsed = ShareCardOptions.Parse(allOff.Serialize());

        Assert.Equal(allOff, parsed);
        Assert.NotEqual(ShareCardOptions.Default, parsed);
    }

    [Fact]
    public void EveryFlagSurvivesTheRoundTrip()
    {
        var everything = new ShareCardOptions
        {
            SongNames = true, LetterGrades = true, Plates = true, Scores = true,
            IncludeBrokenScores = true, Pumbility = true, ExpectedGains = true, Skills = true,
            BoundaryTodo = true, BoundaryPass = true, ColorByLetterGrade = true, ColorByPlate = true,
            BoundaryOtherMixes = true, BoundaryBroken = true, BoundaryTop50 = true
        };

        Assert.Equal(everything, ShareCardOptions.Parse(everything.Serialize()));
    }

    [Fact]
    public void UnknownTokensAreIgnoredSoANewerSaveStillReads()
    {
        var parsed = ShareCardOptions.Parse("v1,Scores,SomeFutureFlag");

        Assert.True(parsed.Scores);
        Assert.False(parsed.SongNames);
    }

    [Fact]
    public void AValueWithoutTheVersionTokenFallsToTheDefaults()
    {
        Assert.Equal(ShareCardOptions.Default, ShareCardOptions.Parse("Scores,Pumbility"));
    }
}
