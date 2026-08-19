using System;
using System.Collections.Generic;
using Bunit;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     A chart's journey, one row per journaled play. A stage break is a row like any other,
///     saying what it was where a score would sit and never wearing a grade.
/// </summary>
public sealed class ScoreJourneyListTests : ComponentTestBase
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 8, 7, 3, 7, 34, TimeSpan.Zero);

    public ScoreJourneyListTests() => this.RenderInteractive();

    [Fact]
    public void AStageBreakRowSaysSoAndHowFarWhenTheRowIsInThePagesMix()
    {
        var entries = new[]
        {
            StageBreak(MixEnum.Phoenix2, new JudgementCounts(244, 5, 2, 1, 110)),
            Pass(MixEnum.Phoenix2, 959886, At.AddDays(11))
        };

        var list = RenderComponent<ScoreJourneyList>(p => p
            .Add(l => l.Entries, entries)
            .Add(l => l.PageMix, MixEnum.Phoenix2)
            .Add(l => l.NoteCount, 1163));

        Assert.Contains("Stage break · 31% in", list.Markup);
        Assert.Contains("959,886", list.Markup);
        // The pass wears its grade; the stage break wears none.
        Assert.Contains("score-journey-stagebreak", list.Markup);
        Assert.Single(list.FindAll(".score-journey-stagebreak"));
    }

    [Fact]
    public void AStageBreakFromAnotherMixOrWithoutACountKeepsThePlainPhrase()
    {
        // The note count is the page mix's; a Phoenix row on a Phoenix 2 page cannot borrow it.
        var otherMix = RenderComponent<ScoreJourneyList>(p => p
            .Add(l => l.Entries, new[] { StageBreak(MixEnum.Phoenix, new JudgementCounts(244, 5, 2, 1, 110)) })
            .Add(l => l.PageMix, MixEnum.Phoenix2)
            .Add(l => l.NoteCount, 1163));
        var noCount = RenderComponent<ScoreJourneyList>(p => p
            .Add(l => l.Entries, new[] { StageBreak(MixEnum.Phoenix2, new JudgementCounts(244, 5, 2, 1, 110)) })
            .Add(l => l.PageMix, MixEnum.Phoenix2));
        var noBreakdown = RenderComponent<ScoreJourneyList>(p => p
            .Add(l => l.Entries, new[] { StageBreak(MixEnum.Phoenix2, null) })
            .Add(l => l.PageMix, MixEnum.Phoenix2)
            .Add(l => l.NoteCount, 1163));

        foreach (var list in new[] { otherMix, noCount, noBreakdown })
        {
            Assert.Contains("Stage break", list.Markup);
            Assert.DoesNotContain("% in", list.Markup);
        }
    }

    private static ScoreJournalEntry StageBreak(MixEnum mix, JudgementCounts? judgements)
    {
        return new ScoreJournalEntry(At, ScoreJournalEntry.OfficialImportSource, UserId, ChartId, null, null, true,
            mix, null, judgements, false, IsStageBroken: true);
    }

    private static ScoreJournalEntry Pass(MixEnum mix, int score, DateTimeOffset at)
    {
        return new ScoreJournalEntry(at, ScoreJournalEntry.OfficialImportSource, UserId, ChartId,
            PhoenixScore.From(score), PhoenixPlate.FairGame, false, mix);
    }
}
