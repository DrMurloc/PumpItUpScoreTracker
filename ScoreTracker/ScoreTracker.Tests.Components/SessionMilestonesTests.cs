using System;
using System.Linq;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class SessionMilestonesTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnEstimatedPlaceCollapsesPerBoard()
    {
        var milestones = new[]
        {
            Rank("PUMBILITY Singles", 0, 60, 40),
            Rank("PUMBILITY Singles", 30, 40, 31),
            Rank("PUMBILITY Doubles", 10, 90, 88)
        };

        var collapsed = SessionMilestones.Collapse(milestones);

        var singles = Assert.Single(collapsed, m => m.Detail == "PUMBILITY Singles");
        Assert.Equal(60, singles.OldValue);
        Assert.Equal(31, singles.NewValue);
        Assert.Single(collapsed, m => m.Detail == "PUMBILITY Doubles");
    }

    [Fact]
    public void ASessionThatBeganUnplacedStillReadsAsAFirstPlacing()
    {
        var milestones = new[]
        {
            Rank("PUMBILITY", 0, null, 40),
            Rank("PUMBILITY", 30, 40, 12)
        };

        var rank = Assert.Single(SessionMilestones.Collapse(milestones));

        Assert.Null(rank.OldValue);
        Assert.Equal(12, rank.NewValue);
    }

    [Fact]
    public void AFolderThatMovedInTwoImportsReadsAsOneMove()
    {
        // The tier crossed in the first import, the grade in the second: one line, from where the
        // folder stood before either.
        var milestones = new[]
        {
            Folder(0, new FolderProgressDetail("S22", 80, PhoenixLetterGrade.AAPlus, 60, null)),
            Folder(40, new FolderProgressDetail("S22", 80, PhoenixLetterGrade.AAA, null, PhoenixLetterGrade.AAPlus)),
            Folder(20, new FolderProgressDetail("D23", 40, null, 20, null))
        };

        var collapsed = SessionMilestones.Collapse(milestones);

        Assert.Equal(2, collapsed.Count);
        var s22 = FolderProgressDetail.TryParse(Assert.Single(collapsed,
            m => m.Detail!.StartsWith("S22", StringComparison.Ordinal)).Detail)!;
        Assert.Equal(60, s22.FromTier);
        Assert.Equal(80, s22.Tier);
        Assert.Equal(PhoenixLetterGrade.AAPlus, s22.FromGrade);
        Assert.Equal(PhoenixLetterGrade.AAA, s22.Grade);
    }

    [Fact]
    public void AFolderWhoseGradeFellBackStatesWhereItEndedWithoutAnArrow()
    {
        // The grade rose in one import and a weak new pass carried it back in the next. "AA → AA"
        // would print a standstill as progress; the line states where the folder ended instead.
        var milestones = new[]
        {
            Folder(0, new FolderProgressDetail("S22", 80, PhoenixLetterGrade.AAA, null, PhoenixLetterGrade.AA)),
            Folder(40, new FolderProgressDetail("S22", 100, PhoenixLetterGrade.AA, 80, null))
        };

        var s22 = FolderProgressDetail.TryParse(Assert.Single(SessionMilestones.Collapse(milestones)).Detail)!;

        Assert.Equal(80, s22.FromTier);
        Assert.Equal(100, s22.Tier);
        Assert.Equal(PhoenixLetterGrade.AA, s22.Grade);
        Assert.Null(s22.FromGrade);
    }

    [Fact]
    public void HardmodeCollapsesWhateverStandingEachImportReached()
    {
        // Hardmode's detail is the standing the pool reached, which moves every batch; it must not
        // split the night back into a strip per import.
        var milestones = new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.HardmodePumbilityGain, Guid.NewGuid(), Start, 3300, 3350, null,
                "12|40"),
            new PlayerMilestoneRecord(MilestoneKind.HardmodePumbilityGain, Guid.NewGuid(), Start.AddMinutes(30),
                3350, 3420, null, "9|41")
        };

        var hardmode = Assert.Single(SessionMilestones.Collapse(milestones));

        Assert.Equal(3300, hardmode.OldValue);
        Assert.Equal(3420, hardmode.NewValue);
        Assert.Equal("9|41", hardmode.Detail);
    }

    [Fact]
    public void EventsPassThroughEachOnItsOwn()
    {
        var milestones = new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, Guid.NewGuid(), Start, null, null,
                "Advanced Lv. 3", null),
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, Guid.NewGuid(), Start.AddMinutes(30), null, null,
                "Advanced Lv. 4", null),
            new PlayerMilestoneRecord(MilestoneKind.FolderPassLamp, Guid.NewGuid(), Start, null, null, null, "D23")
        };

        var collapsed = SessionMilestones.Collapse(milestones);

        Assert.Equal(3, collapsed.Count);
        Assert.Equal(2, collapsed.Count(m => m.Kind == MilestoneKind.TitleCompleted));
    }

    private static PlayerMilestoneRecord Rank(string board, int minutes, double? from, double to)
    {
        return new PlayerMilestoneRecord(MilestoneKind.OfficialPumbilityRank, Guid.NewGuid(),
            Start.AddMinutes(minutes), from, to, null, board);
    }

    private static PlayerMilestoneRecord Folder(int minutes, FolderProgressDetail detail)
    {
        return new PlayerMilestoneRecord(MilestoneKind.FolderProgress, Guid.NewGuid(), Start.AddMinutes(minutes),
            null, null, null, detail.Format());
    }
}
