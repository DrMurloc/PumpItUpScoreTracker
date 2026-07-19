namespace ScoreTracker.Tests.Exploration.ScoreImageExtraction;

/// <summary>
///     What the extractor read off one result-screen panel. -1 means "field not found";
///     the diagnostics list says which anchors pass 1 located, so a miss can be told apart
///     as localization (no anchor) vs recognition (anchor found, digits wrong).
/// </summary>
public sealed record ExtractedPanel
{
    public string Image { get; init; } = "";
    public int Score { get; init; } = -1;
    public int Perfect { get; init; } = -1;
    public int Great { get; init; } = -1;
    public int Good { get; init; } = -1;
    public int Bad { get; init; } = -1;
    public int Miss { get; init; } = -1;
    public int MaxCombo { get; init; } = -1;
    public bool Broken { get; init; }
    public List<string> AnchorsFound { get; init; } = new();
    public long ElapsedMs { get; init; }

    /// <summary>
    ///     Phoenix score reconstructed from the judgements (floor — matches real cabinets;
    ///     the shipped ScoreScreen ceils and reads 1 high). -1 when any judgement is missing.
    /// </summary>
    public int ReconstructedScore
    {
        get
        {
            if (Perfect < 0 || Great < 0 || Good < 0 || Bad < 0 || Miss < 0 || MaxCombo < 0) return -1;
            var total = Perfect + Great + Good + Bad + Miss;
            if (total <= 0) return -1;
            return (int)Math.Floor(
                (0.995 * (Perfect + 0.6 * Great + 0.2 * Good + 0.1 * Bad) + 0.005 * MaxCombo)
                / total * 1_000_000.0);
        }
    }

    public bool SelfConsistent => Score > 0 && Score == ReconstructedScore;
}
