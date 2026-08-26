using System;
using System.Collections.Generic;
using System.Linq;
using Bunit;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class SessionHistoryCardsTests : ComponentTestBase
{
    private static readonly DateTimeOffset When = new(2026, 8, 1, 22, 10, 0, TimeSpan.Zero);

    public SessionHistoryCardsTests() => this.RenderInteractive();

    [Fact]
    public void ASessionWithNoMilestonesStillCarriesItsArtAndCounts()
    {
        // Most of a real history predates capture. The card has to read on counts and jackets
        // alone, or seventeen of twenty look broken.
        var cards = Render(new[] { RowFor(headline: Array.Empty<PlayerMilestoneRecord>()) });

        Assert.Single(cards.FindAll("[data-testid='session-card']"));
        Assert.Contains("Recorded before session capture", cards.Markup);
        // Three jackets and the overflow count, from the journal rather than from capture.
        Assert.Contains("+2", cards.Markup);
    }

    [Fact]
    public void AHeadlineReplacesTheQuietLineWhenTheSessionEarnedOne()
    {
        var cards = Render(new[]
        {
            RowFor(headline: new[]
            {
                new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, Guid.NewGuid(), When, null, null,
                    "Advanced Lv. 3", null)
            })
        });

        Assert.Contains("Advanced Lv. 3", cards.Markup);
        Assert.DoesNotContain("Recorded before session capture", cards.Markup);
    }

    [Fact]
    public void PagingHidesItselfOnASinglePage()
    {
        var one = Render(new[] { RowFor(Array.Empty<PlayerMilestoneRecord>()) }, pageCount: 1);
        Assert.Empty(one.FindAll("[data-testid='session-pagination']"));
    }

    [Fact]
    public void AFilterThatMatchesNothingSaysSoRatherThanRenderingAnEmptyGrid()
    {
        var none = Render(Array.Empty<SessionHistoryRow>());
        Assert.NotEmpty(none.FindAll("[data-testid='session-history-empty']"));
    }

    private IRenderedComponent<SessionHistoryCards> Render(IReadOnlyList<SessionHistoryRow> rows,
        int pageCount = 1)
    {
        return RenderComponent<SessionHistoryCards>(p => p
            .Add(c => c.Rows, rows)
            .Add(c => c.Page, 1)
            .Add(c => c.PageCount, pageCount));
    }

    private static SessionHistoryRow RowFor(IReadOnlyList<PlayerMilestoneRecord> headline)
    {
        var charts = Enumerable.Range(20, 3).Select(l => ChartAt(l)).ToArray();
        return new SessionHistoryRow(Guid.NewGuid(), null, MixEnum.Phoenix, "officialImport",
            When.AddHours(-2), When, 2, 6, 16, charts, 2, "S20–S22", headline, "DRMURLOC #7251");
    }

    private static Chart ChartAt(int level)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, ChartType.Single,
            DifficultyLevel.From(level), MixEnum.Phoenix, null, null);
    }
}
