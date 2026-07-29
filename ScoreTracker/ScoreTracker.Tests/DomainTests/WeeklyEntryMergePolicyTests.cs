using System;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The weekly board's write rules (weekly-charts-overhaul.md §9.3). Two callers share one
///     command — the official import, whose replays must never move a board, and the Record
///     dialog, where a player corrects their own self-report.
/// </summary>
public sealed class WeeklyEntryMergePolicyTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Chart = Guid.NewGuid();
    private static readonly Uri Photo = new("https://example.invalid/proof.png");

    private static WeeklyTournamentEntry Entry(int score, PhoenixPlate plate = PhoenixPlate.SuperbGame,
        bool isBroken = false, Uri? photo = null, double competitiveLevel = 20) =>
        new(User, Chart, score, plate, isBroken, photo, competitiveLevel);

    [Fact]
    public void AFirstRecordingIsAlwaysAnImprovement()
    {
        var result = WeeklyEntryMergePolicy.Merge(null, Entry(900000), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.BestWins, 18.5);

        Assert.True(result.IsImprovement);
        Assert.False(result.IsRefused);
        Assert.Equal((PhoenixScore)900000, result.Entry.Score);
        Assert.Equal(ChallengeEntrySource.Manual, result.Source);
        Assert.Equal(18.5, result.Entry.CompetitiveLevel);
    }

    [Fact]
    public void BestWinsKeepsTheHigherStoredScore()
    {
        var stored = (Entry(974220), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(947220), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.BestWins, 20);

        Assert.Equal((PhoenixScore)974220, result.Entry.Score);
        Assert.False(result.IsImprovement);
    }

    [Fact]
    public void BestWinsTakesTheHigherSubmittedScoreAndItsSource()
    {
        var stored = (Entry(900000), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(990000), ChallengeEntrySource.Official,
            WeeklyEntryIntent.BestWins, 20);

        Assert.Equal((PhoenixScore)990000, result.Entry.Score);
        Assert.Equal(ChallengeEntrySource.Official, result.Source);
        Assert.True(result.IsImprovement);
    }

    [Fact]
    public void AWeakerManualSubmissionNeverDemotesAVerifiedScoresTag()
    {
        // The source describes the ranked score's provenance, so it moves only when the
        // score does — otherwise a lowball self-report would strip the ✔ off an import.
        var stored = (Entry(990000), ChallengeEntrySource.Official);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(900000), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.BestWins, 20);

        Assert.Equal(ChallengeEntrySource.Official, result.Source);
    }

    [Fact]
    public void BestWinsKeepsTheBetterPlateAndClearsBroken()
    {
        var stored = (Entry(950000, PhoenixPlate.MarvelousGame, isBroken: true), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored,
            Entry(940000, PhoenixPlate.PerfectGame), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.BestWins, 20);

        Assert.Equal(PhoenixPlate.PerfectGame, result.Entry.Plate);
        Assert.False(result.Entry.IsBroken);
        // The plate improved but the ranked score did not — nothing to celebrate.
        Assert.False(result.IsImprovement);
    }

    [Fact]
    public void APhotolessSubmissionNeverWipesAttachedProof()
    {
        var stored = (Entry(950000, photo: Photo), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(990000), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.BestWins, 20);

        Assert.Equal(Photo, result.Entry.PhotoUrl);
    }

    [Fact]
    public void ReplaceTakesALowerScore()
    {
        var stored = (Entry(974220), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(947220), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.Replace, 20);

        Assert.Equal((PhoenixScore)947220, result.Entry.Score);
        Assert.False(result.IsRefused);
        Assert.False(result.IsImprovement);
    }

    [Fact]
    public void ReplaceTakesAWorsePlateToo()
    {
        // The whole point of an amend is that the submission is the truth — a correction
        // that also fixes an overstated plate must not have the old one merged back in.
        var stored = (Entry(974220, PhoenixPlate.PerfectGame), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored,
            Entry(947220, PhoenixPlate.MarvelousGame), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.Replace, 20);

        Assert.Equal(PhoenixPlate.MarvelousGame, result.Entry.Plate);
    }

    [Fact]
    public void ReplaceIsRefusedAgainstAnOfficiallyImportedEntry()
    {
        var stored = (Entry(981540), ChallengeEntrySource.Official);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(900000), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.Replace, 20);

        Assert.True(result.IsRefused);
        Assert.False(result.IsImprovement);
        Assert.Equal((PhoenixScore)981540, result.Entry.Score);
        Assert.Equal(ChallengeEntrySource.Official, result.Source);
    }

    [Fact]
    public void ReplaceStillKeepsAttachedProofWhenNoNewPhotoIsSupplied()
    {
        var stored = (Entry(974220, photo: Photo), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(947220), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.Replace, 20);

        Assert.Equal(Photo, result.Entry.PhotoUrl);
    }

    [Fact]
    public void ReplaceWithAHigherScoreCountsAsAnImprovement()
    {
        // The dialog only sends Replace downward, but the command is public — a raise
        // arriving under Replace is still a raise.
        var stored = (Entry(900000), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(950000), ChallengeEntrySource.Manual,
            WeeklyEntryIntent.Replace, 20);

        Assert.True(result.IsImprovement);
    }

    [Theory]
    [InlineData(WeeklyEntryIntent.BestWins)]
    [InlineData(WeeklyEntryIntent.Replace)]
    public void TheCompetitiveLevelIsAlwaysRestamped(WeeklyEntryIntent intent)
    {
        // The band verdict has to describe the player today, not whenever the row was
        // first written — the relevant-players filter reads it (§12.3).
        var stored = (Entry(950000, competitiveLevel: 12), ChallengeEntrySource.Manual);

        var result = WeeklyEntryMergePolicy.Merge(stored, Entry(940000, competitiveLevel: 12),
            ChallengeEntrySource.Manual, intent, 21.5);

        Assert.Equal(21.5, result.Entry.CompetitiveLevel);
    }
}
