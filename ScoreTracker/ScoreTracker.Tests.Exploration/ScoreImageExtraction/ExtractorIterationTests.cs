using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Exploration.ScoreImageExtraction;

/// <summary>
///     The self-iteration loop for the score-image extractor. Runs the current extractor over
///     every fixture image on disk, scores each field against the hand-verified ground truth,
///     prints a per-field hit table, and snapshots the raw extraction to Snapshots/latest.json
///     so iterations can be diffed. Skips (passes with a note) when the local images or
///     tessdata are absent — this suite never runs in CI.
/// </summary>
public sealed class ExtractorIterationTests
{
    private readonly ITestOutputHelper _output;

    public ExtractorIterationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Run_extractor_over_fixture_images_and_score()
    {
        if (!ExplorationPaths.InputsAvailable(out var reason))
        {
            _output.WriteLine($"SKIPPED — {reason}");
            return;
        }

        var truths = GroundTruth.LoadAll();
        var images = truths.Select(t => t.Image).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(i => File.Exists(Path.Combine(ExplorationPaths.ImagesDirectory, i)))
            .OrderBy(i => i, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(images);

        using var extractor = new PhoenixScreenExtractor(ExplorationPaths.TessdataDirectory);
        var panels = new List<ExtractedPanel>();
        foreach (var image in images)
        {
            var panel = extractor.Extract(Path.Combine(ExplorationPaths.ImagesDirectory, image));
            panels.Add(panel);
            _output.WriteLine(
                $"extracted {image} in {panel.ElapsedMs}ms  (anchors: {string.Join(",", panel.AnchorsFound)})");
        }

        Report(truths, panels);
        Snapshot(panels);
    }

    private void Report(IReadOnlyList<GroundTruth> truths, List<ExtractedPanel> panels)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("fixture       tag       scr  P  G  Gd B  M  C  brk | recon  notes");
        sb.AppendLine(new string('-', 96));

        var fieldHits = new Dictionary<string, int>();
        var fieldTotals = new Dictionary<string, int>();
        var fullPanels = 0;
        var officialTruths = 0;

        void Tally(string field, bool hit)
        {
            fieldTotals[field] = fieldTotals.GetValueOrDefault(field) + 1;
            if (hit) fieldHits[field] = fieldHits.GetValueOrDefault(field) + 1;
        }

        foreach (var truth in truths)
        {
            var panel = panels.FirstOrDefault(p =>
                p.Image.Equals(truth.Image, StringComparison.OrdinalIgnoreCase));
            if (panel is null) continue;
            if (truth.Source == "official") officialTruths++;

            var hits = new Dictionary<string, bool>
            {
                ["scr"] = panel.Score == truth.ScoreInt,
                ["P"] = panel.Perfect == truth.PerfectInt,
                ["G"] = panel.Great == truth.GreatInt,
                ["Gd"] = panel.Good == truth.GoodInt,
                ["B"] = panel.Bad == truth.BadInt,
                ["M"] = panel.Miss == truth.MissInt,
                ["C"] = panel.MaxCombo == truth.MaxComboInt,
                ["brk"] = panel.Broken == truth.Broken,
            };
            foreach (var (field, hit) in hits) Tally(field, hit);
            if (hits.Values.All(h => h)) fullPanels++;

            var fixtureId = truth.Image.Replace(".jpg", "") + (truth.Player == "" ? "" : "/" + truth.Player);
            sb.AppendLine(
                $"{fixtureId,-13} {Trunc(truth.Gametag, 9),-9} " +
                $"{Mark(hits["scr"]),-4}{Mark(hits["P"]),-3}{Mark(hits["G"]),-3}{Mark(hits["Gd"]),-3}" +
                $"{Mark(hits["B"]),-3}{Mark(hits["M"]),-3}{Mark(hits["C"]),-3}{Mark(hits["brk"]),-4}| " +
                $"{(panel.SelfConsistent ? "✓" : "·"),-6} {Notes(panel, truth)}");
        }

        sb.AppendLine(new string('-', 96));
        sb.AppendLine("per-field accuracy: " + string.Join("  ",
            fieldTotals.Keys.Select(f => $"{f} {fieldHits.GetValueOrDefault(f)}/{fieldTotals[f]}")));
        sb.AppendLine(
            $"fully-correct panels: {fullPanels}/{fieldTotals.GetValueOrDefault("scr")}  " +
            $"(official fixtures: {officialTruths})");
        sb.AppendLine(
            "recon ✓ = extracted judgements reconstruct the extracted score (the no-truth confidence " +
            "signal production would rely on)");
        _output.WriteLine(sb.ToString());
    }

    private static string Mark(bool hit) => hit ? "✓" : "✗";

    private static string Trunc(string value, int max) => value.Length <= max ? value : value[..max];

    private static string Notes(ExtractedPanel panel, GroundTruth truth)
    {
        var notes = new List<string>();
        if (panel.Score >= 0 && panel.Score != truth.ScoreInt)
            notes.Add($"scr read {panel.Score} want {truth.ScoreInt}");
        if (panel.Score < 0) notes.Add("scr not found");
        var missing = new[] { "perfect", "great", "good", "bad", "miss", "combo" }
            .Where(a => !panel.AnchorsFound.Contains(a)).ToList();
        if (missing.Count > 0) notes.Add("no anchor: " + string.Join(",", missing));
        return Trunc(string.Join("; ", notes), 40);
    }

    private void Snapshot(List<ExtractedPanel> panels)
    {
        Directory.CreateDirectory(ExplorationPaths.SnapshotsDirectory);
        var path = Path.Combine(ExplorationPaths.SnapshotsDirectory, "latest.json");
        File.WriteAllText(path,
            JsonSerializer.Serialize(panels, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine($"snapshot written: {path}");
    }
}
