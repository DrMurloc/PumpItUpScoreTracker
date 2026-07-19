using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.ScoreImageExtraction;

/// <summary>
///     PoC harness for the "extract scores from images" experiment. It loads every raw read in
///     Fixtures/, runs it through <see cref="ScoreReconciler" />, and prints a report. The raw
///     reads are transcribed by hand from real cabinet photos; this measures how well the
///     domain-model reconciliation recovers the true score from an imperfect read — the go/no-go
///     signal for building the self-hosted extractor. It is not a ratchet: it documents current
///     behaviour and fails loudly if the reconciliation regresses on this batch.
/// </summary>
public sealed class ReconciliationHarnessTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ITestOutputHelper _output;

    public ReconciliationHarnessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Reconciles_the_photo_batch_and_reports()
    {
        var reads = LoadFixtures();
        Assert.NotEmpty(reads);

        var results = reads
            .Select(ScoreReconciler.Reconcile)
            .OrderBy(r => r.Image, StringComparer.Ordinal)
            .ToList();

        var report = new StringBuilder();
        report.AppendLine();
        report.AppendLine(
            "image      src      tag       pl brk  song                  diff  printed   recomp  delta  status");
        report.AppendLine(new string('-', 110));
        foreach (var r in results)
            report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-10} {1,-8} {2,-9} {3,-2} {4,-3}  {5,-21} {6,-5} {7,7} {8,8}  {9,5}  {10}{11}",
                r.Image,
                r.Source,
                Truncate(r.Gametag, 9),
                r.Player,
                r.Broken ? "BRK" : "",
                Truncate(r.Song, 21),
                r.Difficulty,
                r.PrintedScore,
                r.FloorScore,
                r.Delta,
                r.Status,
                r.Status == ReconciliationStatus.Flagged ? "  <== confirm" : ""));
        report.AppendLine(new string('-', 110));

        // Official screens are the accuracy target. Sims (StepP2 etc.) are kept for reference but
        // are NOT trained around and NOT rejected — reported separately so they never distort the
        // official metric.
        var official = results.Where(r => r.Source == "official").ToList();
        var sims = results.Where(r => r.Source != "official").ToList();

        var officialExact = official.Count(r => r.Status == ReconciliationStatus.Exact);
        var officialClose = official.Count(r => r.Status == ReconciliationStatus.Close);
        var officialFlagged = official.Count(r => r.Status == ReconciliationStatus.Flagged);
        var officialFloor = official.Count(r => r.RoundingMatch == "floor");
        var officialCeil = official.Count(r => r.RoundingMatch == "ceil");

        var passes = official.Count(r => !r.Broken);
        var breaks = official.Count(r => r.Broken);
        var breaksReconciled = official.Count(r => r.Broken && r.Status != ReconciliationStatus.Flagged);
        report.AppendLine(
            $"OFFICIAL ({official.Count}):  {officialExact} exact, {officialClose} close (<=2pts), " +
            $"{officialFlagged} flagged   |   rounding: {officialFloor} FLOOR, {officialCeil} CEIL " +
            "(shipped ScoreScreen ceils — real screens floor)");
        report.AppendLine(
            $"  pass vs break: {passes} passes, {breaks} breaks — {breaksReconciled}/{breaks} broken plays " +
            "reconcile too (broken is orthogonal to the score; read from missing plate, never derived)");
        report.AppendLine(
            $"SIMS ({sims.Count}, informational, not a target):  " +
            string.Join(", ", sims.Select(s => $"{s.Song}={s.Status}")));
        _output.WriteLine(report.ToString());

        // Every official screen must reconcile. If reconciliation regresses on official screens,
        // this fails. Sims are deliberately excluded from the assertion.
        Assert.True(officialExact + officialClose == official.Count,
            $"Every official screen should reconcile; {officialFlagged} flagged.");
        // The cabinets floor; the shipped formula ceils. Assert the finding holds on the official set.
        Assert.True(officialFloor > officialCeil,
            "Expected official printed scores to match FLOOR more than CEIL.");
    }

    private static List<RawScoreRead> LoadFixtures()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "ScoreImageExtraction", "Fixtures");
        Assert.True(Directory.Exists(dir), $"Fixtures folder not found at {dir} (check csproj Content copy).");

        return Directory.EnumerateFiles(dir, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<RawScoreRead>(File.ReadAllText(f), JsonOptions)!)
            .ToList();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
