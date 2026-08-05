using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class VanishedTagAnalyzerTests
{
    private const int SnapshotId = 5;
    private const int RatingBoardId = 99;
    private const int OldId = 1;
    private const int NewId = 2;
    private static readonly Uri OneAvatar = new("https://example.invalid/av1.png");
    private static readonly Uri AnotherAvatar = new("https://example.invalid/av2.png");

    private static readonly BoardDimension[] Boards = Enumerable.Range(1, 40)
        .Select(id => new BoardDimension(id, LeaderboardTypes.Chart, $"Board {id}", Guid.NewGuid(), "Single", 24))
        .Append(new BoardDimension(RatingBoardId, LeaderboardTypes.Rating, "PUMBILITY", null, null, null))
        .ToArray();

    private static PlayerDimension Player(int id, string username, Uri? avatar = null) =>
        new(id, username, avatar, null);

    /// <summary>One row per board, each board a different score, so an exact match means something.</summary>
    private static PlacementRow[] Rows(int playerId, int fromBoard, int count, decimal baseScore = 950000) =>
        Enumerable.Range(fromBoard, count)
            .Select(boardId => new PlacementRow(boardId, playerId, 3, baseScore + boardId))
            .ToArray();

    /// <summary>
    ///     Bystanders holding a board open. They sit in BOTH snapshots so they are never
    ///     mistaken for tags that appeared this week.
    /// </summary>
    private static PlacementRow[] Crowd(int boardId, int count, decimal topScore = 999000) =>
        Enumerable.Range(0, count)
            .Select(i => new PlacementRow(boardId, 1000 + i, i + 1, topScore - i * 100))
            .ToArray();

    private static RenameProposal Single(IReadOnlyList<PlacementRow> current,
        IReadOnlyList<PlacementRow> previous, params PlayerDimension[] players) =>
        Assert.Single(VanishedTagAnalyzer.Analyze(SnapshotId, players, Boards, current, previous));

    [Fact]
    public void ExactScoresUnderANewTagMergeWithoutAnAdmin()
    {
        var finding = Single(Rows(NewId, 1, 8), Rows(OldId, 1, 8),
            Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(VanishVerdicts.Merge, finding.Verdict);
        Assert.Equal(NewId, finding.NewPlayerId);
        Assert.Equal("OLDTAG", finding.OldUsername);
        Assert.Equal("NEWTAG", finding.NewUsername);
        Assert.Equal(8, finding.Evidence.ExactNonPgMatches);
        Assert.Equal(8, finding.Evidence.BoardsPresent);
        Assert.Equal(SnapshotId, finding.CreatedSnapshotId);
    }

    [Fact]
    public void ANewAvatarDoesNotHideARename()
    {
        // The single most common shape this has to catch: a player changes their name and
        // their picture in the same sitting. Gating on the avatar missed most renames.
        var finding = Single(Rows(NewId, 1, 8), Rows(OldId, 1, 8),
            Player(OldId, "OLDTAG", OneAvatar), Player(NewId, "NEWTAG", AnotherAvatar));

        Assert.Equal(VanishVerdicts.Merge, finding.Verdict);
        Assert.False(finding.Evidence.AvatarMatched);
    }

    [Fact]
    public void AMatchingAvatarIsRecordedAsEvidence()
    {
        var finding = Single(Rows(NewId, 1, 8), Rows(OldId, 1, 8),
            Player(OldId, "OLDTAG", OneAvatar), Player(NewId, "NEWTAG", OneAvatar));

        Assert.True(finding.Evidence.AvatarMatched);
    }

    [Fact]
    public void ALowerScoreOnAnySingleBoardDisqualifiesTheCandidate()
    {
        // Mirrored bests only ever improve, so one board going backwards means this is a
        // different human — however well the other seven line up.
        var current = Rows(NewId, 1, 7)
            .Append(new PlacementRow(8, NewId, 3, 1))
            .ToArray();

        var finding = Single(current, Rows(OldId, 1, 8), Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(VanishVerdicts.DroppedOff, finding.Verdict);
        Assert.Null(finding.NewPlayerId);
    }

    [Fact]
    public void PerfectGamesAloneIdentifyNobody()
    {
        var previous = Enumerable.Range(1, 6)
            .Select(b => new PlacementRow(b, OldId, 1, 1_000_000m)).ToArray();
        var current = Enumerable.Range(1, 6)
            .Select(b => new PlacementRow(b, NewId, 1, 1_000_000m)).ToArray();

        var finding = Single(current, previous, Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(0, finding.Evidence.ExactNonPgMatches);
        Assert.Equal(6, finding.Evidence.ExactPerfectGames);
        Assert.Equal(VanishVerdicts.DroppedOff, finding.Verdict);
    }

    [Fact]
    public void AScoreThatShouldStillBeRankingGoesToAnAdmin()
    {
        // Board 1 is 60 deep and the old tag's score would still place eleventh. Nobody is
        // standing there — that is not a rename, and a ban is the usual answer.
        var previous = Rows(OldId, 2, 6).Append(new PlacementRow(1, OldId, 10, 998050)).ToArray();
        var current = Rows(NewId, 2, 6).Concat(Crowd(1, 60)).ToArray();

        var finding = Single(current, previous, Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(VanishVerdicts.Suspicious, finding.Verdict);
        Assert.Equal(1, finding.Evidence.SuspiciousAbsences);
    }

    [Fact]
    public void AnAbsenceAtTheTailOfABoardIsJitterAndIsIgnored()
    {
        // Same shape, except the old score would now rank fiftieth of sixty. Boards are paged
        // until the site stops serving rows and the last row moves between runs; every
        // apparent disappearance on record sat within a few places of the tail.
        var previous = Rows(OldId, 2, 6).Append(new PlacementRow(1, OldId, 50, 994150)).ToArray();
        var current = Rows(NewId, 2, 6).Concat(Crowd(1, 60)).ToArray();

        var finding = Single(current, previous, Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(0, finding.Evidence.SuspiciousAbsences);
        Assert.Equal(VanishVerdicts.Merge, finding.Verdict);
    }

    [Fact]
    public void TwoComparableCandidatesAreNeverGuessedBetween()
    {
        var current = Rows(NewId, 1, 8).Concat(Rows(3, 1, 6)).ToArray();

        var finding = Single(current, Rows(OldId, 1, 8),
            Player(OldId, "OLDTAG"), Player(NewId, "CLOSE_A"), Player(3, "CLOSE_B"));

        Assert.Equal(VanishVerdicts.Ambiguous, finding.Verdict);
        Assert.Equal(8, finding.Evidence.ExactNonPgMatches);
        Assert.Equal(6, finding.Evidence.RunnerUpExactMatches);
    }

    [Fact]
    public void AnOverwhelmingLeaderSettlesItAlone()
    {
        // Five against twenty-five is not a contest worth an admin's afternoon.
        var current = Rows(NewId, 1, 25).Concat(Rows(3, 1, 5)).ToArray();

        var finding = Single(current, Rows(OldId, 1, 25),
            Player(OldId, "OLDTAG"), Player(NewId, "OBVIOUS"), Player(3, "COINCIDENCE"));

        Assert.Equal(VanishVerdicts.Merge, finding.Verdict);
        Assert.Equal(NewId, finding.NewPlayerId);
        Assert.Equal(5, finding.Evidence.RunnerUpExactMatches);
    }

    [Fact]
    public void AFittingButThinCandidateIsProposedRatherThanMerged()
    {
        // Present on three boards, one of them an exact match, nothing contradicting: worth
        // a look, not worth a one-way merge.
        var current = new[]
        {
            new PlacementRow(1, NewId, 1, 950001),
            new PlacementRow(2, NewId, 1, 999999),
            new PlacementRow(3, NewId, 1, 999999)
        };

        var finding = Single(current, Rows(OldId, 1, 6), Player(OldId, "OLDTAG"), Player(NewId, "MAYBE"));

        Assert.Equal(VanishVerdicts.Propose, finding.Verdict);
        Assert.Equal(1, finding.Evidence.ExactNonPgMatches);
        Assert.Equal(3, finding.Evidence.BoardsPresent);
    }

    [Fact]
    public void ATagThatSimplyGotPassedIsRecordedNotActioned()
    {
        var finding = Single(Array.Empty<PlacementRow>(), Rows(OldId, 1, 6), Player(OldId, "OLDTAG"));

        Assert.Equal(VanishVerdicts.DroppedOff, finding.Verdict);
        Assert.Null(finding.NewPlayerId);
        Assert.Null(finding.NewUsername);
        Assert.Equal(6, finding.Evidence.OldPlacements);
    }

    [Fact]
    public void ATagStillHoldingARatingRowHasNotLeft()
    {
        var current = Rows(NewId, 1, 8).Append(new PlacementRow(RatingBoardId, OldId, 40, 15000)).ToArray();

        Assert.Empty(VanishedTagAnalyzer.Analyze(SnapshotId,
            new[] { Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG") }, Boards, current, Rows(OldId, 1, 8)));
    }

    [Fact]
    public void ATagWithTooLittleHistoryIsNotWorthExplaining()
    {
        Assert.Empty(VanishedTagAnalyzer.Analyze(SnapshotId,
            new[] { Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG") }, Boards,
            Rows(NewId, 1, 4), Rows(OldId, 1, 4)));
    }

    [Fact]
    public void ATagAlreadyOnTheBoardsLastWeekIsNotACandidate()
    {
        // The new tag was already placing before the old one left, so it is somebody else
        // who happens to share boards — not the same player under a new name.
        var previous = Rows(OldId, 1, 8).Concat(Rows(NewId, 20, 1)).ToArray();

        var finding = Single(Rows(NewId, 1, 8), previous, Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(VanishVerdicts.DroppedOff, finding.Verdict);
        Assert.Null(finding.NewPlayerId);
    }

    [Fact]
    public void ExactlyTheThresholdMerges()
    {
        var finding = Single(Rows(NewId, 1, VanishedTagAnalyzer.ExactMatchesToMerge),
            Rows(OldId, 1, VanishedTagAnalyzer.ExactMatchesToMerge),
            Player(OldId, "OLDTAG"), Player(NewId, "NEWTAG"));

        Assert.Equal(VanishVerdicts.Merge, finding.Verdict);
        Assert.Equal(VanishedTagAnalyzer.ExactMatchesToMerge, finding.Evidence.ExactNonPgMatches);
    }
}
