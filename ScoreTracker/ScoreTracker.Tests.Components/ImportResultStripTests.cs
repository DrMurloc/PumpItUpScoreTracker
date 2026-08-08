using System;
using Bunit;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The strip that answers "did my last import work?" above the form that starts the next one.
///     Four states, and the fourth — a run that never reported back — is the one a boolean outcome
///     could not have expressed.
/// </summary>
public sealed class ImportResultStripTests : ComponentTestBase
{
    private static readonly DateTimeOffset Started = new(2026, 8, 8, 2, 39, 0, TimeSpan.Zero);

    private static ImportAttemptRecord Attempt(ImportOutcome? outcome, TimeSpan? took = null,
        int? scoreCount = null)
    {
        return new ImportAttemptRecord(Guid.NewGuid(), MixEnum.Phoenix2, ImportKind.Standard, Started,
            took is null ? null : Started + took.Value, outcome, Guid.NewGuid(), scoreCount);
    }

    private IRenderedComponent<ImportResultStrip> Render(ImportAttemptRecord? attempt, bool canRetry = true)
    {
        return RenderComponent<ImportResultStrip>(p => p
            .Add(c => c.Attempt, attempt)
            .Add(c => c.CanRetry, canRetry));
    }

    [Fact]
    public void ShowsNothingBeforeAPlayersFirstImport()
    {
        Assert.Empty(Render(null).FindAll("[data-testid=import-result-strip]"));
    }

    [Fact]
    public void ASuccessfulImportPrintsItsScoreCount()
    {
        var strip = Render(Attempt(ImportOutcome.Completed, TimeSpan.FromMinutes(2), 37));

        Assert.Contains("37", strip.Find("[data-testid=import-result-summary]").TextContent);
    }

    /// <summary>
    ///     Finding nothing new is the ordinary result of importing twice in a row. It must not
    ///     print "0 scores", which reads as a failure — it says only that the run finished.
    /// </summary>
    [Fact]
    public void ASuccessfulImportWithNoCountDoesNotClaimZero()
    {
        // Asserted on the word rather than the digit: the timestamp is full of zeroes.
        var strip = Render(Attempt(ImportOutcome.Completed, TimeSpan.FromMinutes(2)));

        Assert.DoesNotContain("scores", strip.Find("[data-testid=import-result-summary]").TextContent,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void APiuGameFailureNamesPiuGameAndOffersARetry()
    {
        var strip = Render(Attempt(ImportOutcome.PiuGameError, TimeSpan.FromMinutes(2)));

        Assert.Contains("PIUGame.com", strip.Find("[data-testid=import-result-summary]").TextContent);
        Assert.Single(strip.FindAll("[data-testid=import-result-retry]"));
    }

    /// <summary>
    ///     The reason CredentialRejected exists as its own outcome: this copy tells the player to
    ///     fix something, where the PiuGameError copy tells them to wait. Folding the two together
    ///     would send a mistyped password the wrong instruction.
    /// </summary>
    [Fact]
    public void ARejectedSignInSaysSoRatherThanBlamingAnOutage()
    {
        var refused = Render(Attempt(ImportOutcome.CredentialRejected, TimeSpan.FromSeconds(20)))
            .Find("[data-testid=import-result-summary]").TextContent;
        var outage = Render(Attempt(ImportOutcome.PiuGameError, TimeSpan.FromMinutes(2)))
            .Find("[data-testid=import-result-summary]").TextContent;

        Assert.NotEqual(outage, refused);
        Assert.Contains("sign-in", refused, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OurOwnFailureOwnsItRatherThanNamingPiuGame()
    {
        var strip = Render(Attempt(ImportOutcome.PiuScoresError, TimeSpan.FromSeconds(41)));

        var text = strip.Find("[data-testid=import-result-summary]").TextContent;
        Assert.Contains("PIU Scores", text);
        Assert.DoesNotContain("PIUGame.com", text);
    }

    /// <summary>
    ///     No FinishedAt and no Outcome is a run nothing ever closed — every deploy landing
    ///     mid-import leaves one. It must read as neither a success nor somebody's fault.
    /// </summary>
    [Fact]
    public void ARunThatNeverReportedBackIsItsOwnState()
    {
        var strip = Render(Attempt(null));

        var text = strip.Find("[data-testid=import-result-summary]").TextContent;
        Assert.Contains("never reported back", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PIUGame.com", text);
        Assert.DoesNotContain("PIU Scores", text);
    }

    [Fact]
    public void TheRetryIsHiddenWhenThePageCannotImport()
    {
        var strip = Render(Attempt(ImportOutcome.PiuGameError, TimeSpan.FromMinutes(2)), canRetry: false);

        Assert.Empty(strip.FindAll("[data-testid=import-result-retry]"));
    }

    /// <summary>A clean run stays a single quiet line — no eyebrow, no retry button.</summary>
    [Fact]
    public void ASuccessDoesNotShout()
    {
        var strip = Render(Attempt(ImportOutcome.Completed, TimeSpan.FromMinutes(2), 5));

        Assert.Empty(strip.FindAll("[data-testid=import-result-retry]"));
    }
}
