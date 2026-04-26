using System;
using System.Linq;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class TournamentSessionTests
{
    [Fact]
    public void NewSessionStartsNeedingApprovalWithNoEntries()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());

        Assert.True(session.NeedsApproval);
        Assert.Null(session.ApprovedVerificationType);
        Assert.Empty(session.Entries);
        Assert.Equal(0, session.CurrentScore);
        Assert.Equal(0, session.TotalScore);
    }

    [Fact]
    public void ApproveClearsNeedsApprovalAndSnapshotsVerificationType()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        session.SetVerificationType(SubmissionVerificationType.Photo);
        Assert.True(session.NeedsApproval);

        session.Approve();

        Assert.False(session.NeedsApproval);
        Assert.Equal(SubmissionVerificationType.Photo, session.ApprovedVerificationType);
    }

    [Fact]
    public void AddPhotoReFlagsNeedsApprovalEvenAfterApprove()
    {
        var session = ApprovedSession();

        session.AddPhoto(new Uri("https://example.invalid/p.png"));

        Assert.True(session.NeedsApproval);
        Assert.Single(session.PhotoUrls);
    }

    [Fact]
    public void RemovePhotoReFlagsNeedsApproval()
    {
        var photo = new Uri("https://example.invalid/p.png");
        var session = ApprovedSession();
        session.AddPhoto(photo);
        session.Approve();

        session.RemovePhoto(photo);

        Assert.True(session.NeedsApproval);
        Assert.Empty(session.PhotoUrls);
    }

    [Theory]
    [InlineData(SubmissionVerificationType.InPerson)]
    [InlineData(SubmissionVerificationType.Unverified)]
    public void SetVerificationTypeAutoApprovesForInPersonAndUnverified(SubmissionVerificationType type)
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());

        session.SetVerificationType(type);

        Assert.False(session.NeedsApproval);
        Assert.Equal(type, session.VerificationType);
    }

    [Theory]
    [InlineData(SubmissionVerificationType.Photo)]
    [InlineData(SubmissionVerificationType.Video)]
    public void SetVerificationTypeRequiresApprovalForPhotoAndVideoWhenNeverApproved(SubmissionVerificationType type)
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());

        session.SetVerificationType(type);

        Assert.True(session.NeedsApproval);
        Assert.Equal(type, session.VerificationType);
    }

    [Fact]
    public void SetVerificationTypeMatchingPreviouslyApprovedTypeDoesNotNeedApproval()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        session.SetVerificationType(SubmissionVerificationType.Photo);
        session.Approve();
        session.AddPhoto(new Uri("https://example.invalid/p.png")); // re-flags NeedsApproval

        session.SetVerificationType(SubmissionVerificationType.Photo);

        Assert.False(session.NeedsApproval);
    }

    [Fact]
    public void SetVerificationTypeDifferentFromPreviouslyApprovedTypeNeedsApproval()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        session.SetVerificationType(SubmissionVerificationType.Photo);
        session.Approve();

        session.SetVerificationType(SubmissionVerificationType.Video);

        Assert.True(session.NeedsApproval);
    }

    [Fact]
    public void CanAddReturnsFalseWhenScorelessScoreIsZero()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        // SinglePerformance has a 0.0 ChartTypeModifier in the default ScoringConfiguration.
        var chart = new ChartBuilder().WithType(ChartType.SinglePerformance).Build();

        Assert.False(session.CanAdd(chart));
    }

    [Fact]
    public void CanAddReturnsFalseWhenAddingChartWouldExceedMaxTime()
    {
        var config = Config();
        config.MaxTime = TimeSpan.FromMinutes(2);
        var session = new TournamentSession(Guid.NewGuid(), config);
        var first = new ChartBuilder().WithSong(SongOfDuration("song-a", TimeSpan.FromSeconds(90))).Build();
        var second = new ChartBuilder().WithSong(SongOfDuration("song-b", TimeSpan.FromSeconds(90))).Build();
        session.Add(first, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.False(session.CanAdd(second));
    }

    [Fact]
    public void CanAddRespectsAllowRepeatsFalseForSameSongLevelAndType()
    {
        var config = Config();
        config.AllowRepeats = false;
        var session = new TournamentSession(Guid.NewGuid(), config);
        var first = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        // Same song name, level, and type but different chart instance.
        var second = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        session.Add(first, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.False(session.CanAdd(second));
    }

    [Fact]
    public void CanAddAllowsRepeatsWhenConfigEnablesThem()
    {
        var config = Config();
        config.AllowRepeats = true;
        var session = new TournamentSession(Guid.NewGuid(), config);
        var first = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        var second = new ChartBuilder().WithSongName("Repeat").WithLevel(15).WithType(ChartType.Single).Build();
        session.Add(first, 900000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.True(session.CanAdd(second));
    }

    [Fact]
    public void AddFlipsNeedsApprovalAndAppendsEntry()
    {
        var session = ApprovedSession();
        var chart = new ChartBuilder().Build();

        session.Add(chart, 950000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.True(session.NeedsApproval);
        Assert.Single(session.Entries);
    }

    [Fact]
    public void AddThrowsArgumentExceptionForChartThatCannotBeAdded()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        var invalid = new ChartBuilder().WithType(ChartType.SinglePerformance).Build();

        Assert.Throws<ArgumentException>(() =>
            session.Add(invalid, 900000, PhoenixPlate.SuperbGame, isBroken: false));
    }

    [Fact]
    public void AddWithoutApprovalDoesNotFlipNeedsApproval()
    {
        var session = ApprovedSession();
        var chart = new ChartBuilder().Build();

        session.AddWithoutApproval(chart, 950000, PhoenixPlate.SuperbGame, isBroken: false);

        Assert.False(session.NeedsApproval);
        Assert.Single(session.Entries);
    }

    [Fact]
    public void SwapReplacesEntryAndFlipsNeedsApproval()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        var chart = new ChartBuilder().Build();
        session.Add(chart, 800000, PhoenixPlate.FairGame, isBroken: false);
        session.Approve();
        var original = session.Entries.Single();

        session.Swap(original, 990000, PhoenixPlate.PerfectGame, isBroken: false);

        var swapped = session.Entries.Single();
        Assert.Equal((PhoenixScore)990000, swapped.Score);
        Assert.Equal(PhoenixPlate.PerfectGame, swapped.Plate);
        Assert.True(session.NeedsApproval);
    }

    [Fact]
    public void SwapIsNoOpWhenEntryNotInList()
    {
        var session = ApprovedSession();
        var chart = new ChartBuilder().Build();
        var stranger = new TournamentSession.Entry(chart, 900000, PhoenixPlate.SuperbGame,
            IsBroken: false, SessionScore: 1, BonusPoints: 0);

        session.Swap(stranger, 1000000, PhoenixPlate.PerfectGame, isBroken: false);

        Assert.Empty(session.Entries);
        Assert.False(session.NeedsApproval);
    }

    [Fact]
    public void RemoveRemovesEntryAndFlipsNeedsApproval()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        session.Add(new ChartBuilder().Build(), 900000, PhoenixPlate.SuperbGame, isBroken: false);
        session.Approve();
        var entry = session.Entries.Single();

        session.Remove(entry);

        Assert.Empty(session.Entries);
        Assert.True(session.NeedsApproval);
    }

    [Fact]
    public void TotalScoreReflectsAddedEntriesWhenStartedEmpty()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        Assert.Equal(0, session.TotalScore);

        session.Add(new ChartBuilder().WithSongName("a").Build(), 950000, PhoenixPlate.SuperbGame, isBroken: false);
        var afterFirst = session.TotalScore;
        session.Add(new ChartBuilder().WithSongName("b").Build(), 990000, PhoenixPlate.PerfectGame, isBroken: false);
        var afterSecond = session.TotalScore;

        Assert.True(afterFirst > 0);
        Assert.True(afterSecond > afterFirst);
        // CurrentScore is captured only by the entries-overload constructor — Add does not update it.
        Assert.Equal(0, session.CurrentScore);
    }

    [Fact]
    public void EntriesOverloadConstructorComputesCurrentScoreFromEntries()
    {
        var entries = new[]
        {
            new TournamentSession.Entry(new ChartBuilder().Build(), 900000, PhoenixPlate.SuperbGame,
                IsBroken: false, SessionScore: 100, BonusPoints: 0),
            new TournamentSession.Entry(new ChartBuilder().Build(), 950000, PhoenixPlate.SuperbGame,
                IsBroken: false, SessionScore: 250, BonusPoints: 0)
        };

        var session = new TournamentSession(Guid.NewGuid(), Config(), entries);

        Assert.Equal(350, session.CurrentScore);
        Assert.Equal(350, session.TotalScore);
    }

    private static TournamentConfiguration Config() =>
        new(new ScoringConfiguration());

    private static TournamentSession ApprovedSession()
    {
        var session = new TournamentSession(Guid.NewGuid(), Config());
        session.SetVerificationType(SubmissionVerificationType.InPerson);
        return session;
    }

    private static Song SongOfDuration(string name, TimeSpan duration) =>
        new(Name.From(name), SongType.Arcade, new Uri("https://example.invalid/song.png"),
            duration, Name.From("artist"), Bpm: null);
}
