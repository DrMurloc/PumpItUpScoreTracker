using System.Text.Json;

namespace ScoreTracker.Tests.Exploration.ScoreImageExtraction;

/// <summary>
///     One hand-verified panel from the fixture set in ScoreTracker.Tests (linked into this
///     project's output). These are the answer key the extractor is scored against.
/// </summary>
public sealed record GroundTruth
{
    public string Image { get; init; } = "";
    public string Source { get; init; } = "official";
    public string Gametag { get; init; } = "";
    public string Player { get; init; } = "";
    public bool Broken { get; init; }
    public string Song { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string PrintedScore { get; init; } = "";
    public string Perfect { get; init; } = "";
    public string Great { get; init; } = "";
    public string Good { get; init; } = "";
    public string Bad { get; init; } = "";
    public string Miss { get; init; } = "";
    public string MaxCombo { get; init; } = "";

    public int ScoreInt => ParseInt(PrintedScore);
    public int PerfectInt => ParseInt(Perfect);
    public int GreatInt => ParseInt(Great);
    public int GoodInt => ParseInt(Good);
    public int BadInt => ParseInt(Bad);
    public int MissInt => ParseInt(Miss);
    public int MaxComboInt => ParseInt(MaxCombo);

    private static int ParseInt(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? 0 : int.Parse(digits);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<GroundTruth> LoadAll()
    {
        return Directory.EnumerateFiles(ExplorationPaths.FixturesDirectory, "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<GroundTruth>(File.ReadAllText(f), JsonOptions)!)
            .ToList();
    }
}
