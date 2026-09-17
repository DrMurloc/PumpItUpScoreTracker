using System;
using System.Linq;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     What "Hardmode off" strips (docs/design/hardmode-leaderboard.md D30) — exactly the Hardmode
///     facts, and never a neighbour that shares a row with them.
/// </summary>
public sealed class HardmodeVisibilityTests
{
    private static readonly DateTimeOffset When = new(2026, 9, 15, 11, 14, 0, TimeSpan.Zero);

    [Fact]
    public void TheSkullComesOffAndTheCrownStays()
    {
        var flags = HighlightFlags.PumbilityTop50 | HighlightFlags.HardmodeTop50 | HighlightFlags.OfficialBoardPlacement;

        Assert.Equal(HighlightFlags.PumbilityTop50 | HighlightFlags.OfficialBoardPlacement,
            HardmodeVisibility.Strip(flags));
    }

    [Fact]
    public void AScoreWhoseOnlyFlagWasTheSkullComesBackUnflagged()
    {
        Assert.Equal(HighlightFlags.None, HardmodeVisibility.Strip(HighlightFlags.HardmodeTop50));
    }

    [Fact]
    public void TheHardmodeRankAndGainGoAndEveryOtherDetailStays()
    {
        var detail = new HighlightDetail(PumbilityRank: 28, OfficialPlace: 6, PumbilityGain: 2.68,
            HardmodeGain: 354.24, HardmodeRank: 1);

        var stripped = HardmodeVisibility.Strip(detail)!;

        Assert.Null(stripped.HardmodeGain);
        Assert.Null(stripped.HardmodeRank);
        Assert.Equal(detail with { HardmodeGain = null, HardmodeRank = null }, stripped);
    }

    [Fact]
    public void OnlyTheThreeHardmodeMilestonesAreRemoved()
    {
        var milestones = Enum.GetValues<MilestoneKind>()
            .Select(k => new PlayerMilestoneRecord(k, null, When, 1, 2, null, null))
            .ToArray();

        var kept = HardmodeVisibility.Strip(milestones).Select(m => m.Kind).ToArray();

        Assert.Equal(milestones.Length - 3, kept.Length);
        Assert.DoesNotContain(MilestoneKind.HardmodePumbilityGain, kept);
        Assert.DoesNotContain(MilestoneKind.HardmodeSinglesPumbilityGain, kept);
        Assert.DoesNotContain(MilestoneKind.HardmodeDoublesPumbilityGain, kept);
    }

    [Fact]
    public void AWholeBatchIsStrippedAndEverythingElseAboutItIsUntouched()
    {
        var chartId = Guid.NewGuid();
        var e = ScoreHighlightsCapturedEvent.Create(When, Guid.NewGuid(), MixEnum.Phoenix2, Guid.NewGuid(),
            new[]
            {
                new ScoreHighlightsCapturedEvent.HighlightedChange(chartId, true, null, 980_993, "Marvelous Game",
                    false, HighlightFlags.PumbilityTop50 | HighlightFlags.HardmodeTop50,
                    new HighlightDetail(PumbilityRank: 28, HardmodeRank: 1, HardmodeGain: 354.24))
            },
            new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, null, When, 17738.06, 17764.06, null, null),
                new PlayerMilestoneRecord(MilestoneKind.HardmodePumbilityGain, null, When, 3339.22, 11131.72, null,
                    "25|235")
            });

        var stripped = HardmodeVisibility.Strip(e);

        Assert.Equal(e.EventId, stripped.EventId);
        var change = Assert.Single(stripped.Changes);
        Assert.Equal(HighlightFlags.PumbilityTop50, change.Flags);
        Assert.Equal(28, change.Detail!.PumbilityRank);
        Assert.Null(change.Detail.HardmodeRank);
        Assert.Equal(MilestoneKind.PumbilityGain, Assert.Single(stripped.Milestones).Kind);
    }
}
