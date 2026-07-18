using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class RenameProposalDetectorTests
{
    private const int SnapshotId = 5;
    private static readonly Uri SharedAvatar = new("https://example.invalid/av1.png");
    private static readonly Uri OtherAvatar = new("https://example.invalid/av2.png");

    private static readonly BoardDimension[] Boards = Enumerable.Range(1, 15)
        .Select(id => new BoardDimension(id, LeaderboardTypes.Chart, $"Board {id}", Guid.NewGuid(), "Single", 24))
        .Append(new BoardDimension(99, LeaderboardTypes.Rating, "PUMBILITY", null, null, null))
        .ToArray();

    private static PlayerDimension Player(int id, string username, Uri? avatar) =>
        new(id, username, avatar, null);

    private static PlacementRow[] Rows(int playerId, int fromBoard, int count, decimal score = 950000)
    {
        return Enumerable.Range(fromBoard, count)
            .Select(boardId => new PlacementRow(boardId, playerId, 3, score))
            .ToArray();
    }

    [Fact]
    public void DetectsARenameWhenAvatarAndPlacementsCarryOver()
    {
        var players = new[] { Player(1, "OLDTAG", SharedAvatar), Player(2, "NEWTAG", SharedAvatar) };
        var previous = Rows(playerId: 1, fromBoard: 1, count: 12);
        var current = Rows(playerId: 2, fromBoard: 1, count: 12);

        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards, current, previous);

        var proposal = Assert.Single(proposals);
        Assert.Equal(1, proposal.OldPlayerId);
        Assert.Equal(2, proposal.NewPlayerId);
        Assert.Equal("OLDTAG", proposal.OldUsername);
        Assert.Equal("NEWTAG", proposal.NewUsername);
        Assert.Equal(12, proposal.Top50Overlap);
        Assert.Equal(SnapshotId, proposal.CreatedSnapshotId);
    }

    [Fact]
    public void ADifferentAvatarNeverProposes()
    {
        var players = new[] { Player(1, "OLDTAG", SharedAvatar), Player(2, "NEWTAG", OtherAvatar) };

        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards,
            Rows(2, 1, 12), Rows(1, 1, 12));

        Assert.Empty(proposals);
    }

    [Fact]
    public void InsufficientPlacementOverlapNeverProposes()
    {
        var players = new[] { Player(1, "OLDTAG", SharedAvatar), Player(2, "NEWTAG", SharedAvatar) };
        // Only 6 of the old 12 boards reappear — under the 0.7 floor.
        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards,
            Rows(2, 1, 6), Rows(1, 1, 12));

        Assert.Empty(proposals);
    }

    [Fact]
    public void LowerScoresUnderTheNewTagDoNotCountAsOverlap()
    {
        var players = new[] { Player(1, "OLDTAG", SharedAvatar), Player(2, "NEWTAG", SharedAvatar) };
        // Same boards but every score regressed — mirrored bests never go down, so this
        // is a different human with the same avatar, not a rename.
        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards,
            Rows(2, 1, 12, score: 900000), Rows(1, 1, 12, score: 950000));

        Assert.Empty(proposals);
    }

    [Fact]
    public void AThinOldPlayerNeverProposes()
    {
        var players = new[] { Player(1, "OLDTAG", SharedAvatar), Player(2, "NEWTAG", SharedAvatar) };

        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards,
            Rows(2, 1, 9), Rows(1, 1, 9));

        Assert.Empty(proposals);
    }

    [Fact]
    public void APlayerStillPresentAnywhereIsNotARenameCandidate()
    {
        var players = new[] { Player(1, "OLDTAG", SharedAvatar), Player(2, "NEWTAG", SharedAvatar) };
        // The old tag survives on the rating board even though its chart rows vanished.
        var current = Rows(2, 1, 12)
            .Append(new PlacementRow(99, 1, 40, 15000))
            .ToArray();

        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards, current, Rows(1, 1, 12));

        Assert.Empty(proposals);
    }

    [Fact]
    public void AnAmbiguousTieProposesNothing()
    {
        var players = new[]
        {
            Player(1, "OLDTAG", SharedAvatar),
            Player(2, "CLONE_A", SharedAvatar),
            Player(3, "CLONE_B", SharedAvatar)
        };
        var current = Rows(2, 1, 12).Concat(Rows(3, 1, 12)).ToArray();

        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards, current, Rows(1, 1, 12));

        Assert.Empty(proposals);
    }

    [Fact]
    public void TheStrictlyBestCandidateWins()
    {
        var players = new[]
        {
            Player(1, "OLDTAG", SharedAvatar),
            Player(2, "PARTIAL", SharedAvatar),
            Player(3, "FULLMATCH", SharedAvatar)
        };
        var current = Rows(2, 1, 10).Concat(Rows(3, 1, 12)).ToArray();

        var proposals = RenameProposalDetector.Detect(SnapshotId, players, Boards, current, Rows(1, 1, 12));

        Assert.Equal(3, Assert.Single(proposals).NewPlayerId);
    }
}
