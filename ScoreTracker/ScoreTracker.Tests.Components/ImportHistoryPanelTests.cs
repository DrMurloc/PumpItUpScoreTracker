using System;
using System.Linq;
using Bunit;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The collapsed history below the import controls — the half of the record that exists for
///     working out what has been happening across a bad week rather than for the run in front of
///     you.
/// </summary>
public sealed class ImportHistoryPanelTests : ComponentTestBase
{
    private static readonly DateTimeOffset Started = new(2026, 8, 8, 2, 39, 0, TimeSpan.Zero);

    private static ImportAttemptRecord Attempt(ImportOutcome? outcome = ImportOutcome.Completed,
        ImportKind kind = ImportKind.Standard, TimeSpan? took = null, int? scoreCount = null)
    {
        return new ImportAttemptRecord(Guid.NewGuid(), MixEnum.Phoenix2, kind, Started,
            took is null ? null : Started + took.Value, outcome, Guid.NewGuid(), scoreCount);
    }

    private IRenderedComponent<ImportHistoryPanel> Render(params ImportAttemptRecord[] attempts)
    {
        return RenderComponent<ImportHistoryPanel>(p => p.Add(c => c.Attempts, attempts));
    }

    [Fact]
    public void NamesTheActionThatWouldFillItWhenEmpty()
    {
        var panel = Render();

        Assert.Empty(panel.FindAll("[data-testid=import-history-row]"));
        Assert.Contains("import", panel.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RendersOneRowPerAttempt()
    {
        var panel = Render(Attempt(), Attempt(), Attempt());

        Assert.Equal(3, panel.FindAll("[data-testid=import-history-row]").Count);
    }

    /// <summary>
    ///     A run that never got far enough to look has no count, and printing "0" would read as
    ///     "piugame says you have nothing new" — a different and wrong claim.
    /// </summary>
    [Fact]
    public void AnUnknownScoreCountPrintsAnEmDashRatherThanZero()
    {
        var row = Render(Attempt(ImportOutcome.PiuGameError, took: TimeSpan.FromMinutes(2)))
            .Find("[data-testid=import-history-row]");

        Assert.Contains("—", row.TextContent);
        Assert.DoesNotContain(">0<", row.InnerHtml);
    }

    [Fact]
    public void AnUnfinishedRunHasNoDurationAndSaysSo()
    {
        var row = Render(Attempt(null)).Find("[data-testid=import-history-row]");

        Assert.Contains("—", row.TextContent);
    }

    [Fact]
    public void EachKindIsNamedDistinctly()
    {
        var rows = Render(
                Attempt(kind: ImportKind.Standard),
                Attempt(kind: ImportKind.Check),
                Attempt(kind: ImportKind.DeepScan))
            .FindAll("[data-testid=import-history-row]")
            .Select(r => r.TextContent)
            .ToArray();

        Assert.Equal(3, rows.Distinct().Count());
    }

    [Fact]
    public void ARemoteFailureAndOurOwnAreLabelledDifferently()
    {
        var rows = Render(
                Attempt(ImportOutcome.PiuGameError, took: TimeSpan.FromMinutes(2)),
                Attempt(ImportOutcome.PiuScoresError, took: TimeSpan.FromSeconds(41)))
            .FindAll("[data-testid=import-history-row]");

        Assert.Contains("PIUGame.com", rows[0].TextContent);
        Assert.Contains("PIU Scores", rows[1].TextContent);
    }

    [Fact]
    public void ASuccessfulRunPrintsItsScoreCount()
    {
        var row = Render(Attempt(took: TimeSpan.FromMinutes(2), scoreCount: 37))
            .Find("[data-testid=import-history-row]");

        Assert.Contains("37", row.TextContent);
    }
}
