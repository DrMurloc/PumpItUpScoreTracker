using System.Diagnostics.CodeAnalysis;

namespace ScoreTracker.Tests.ScoreImageExtraction;

/// <summary>
///     One result screen as an image extractor reports it — every field a raw string, because
///     that is what OCR (or a vision model) emits before any validation. The reconciler turns
///     these strings into a checked score. In the PoC the strings are transcribed by hand from
///     the photos; in production they would be the extractor's output.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RawScoreRead
{
    public string Image { get; init; } = "";
    public string Camera { get; init; } = "";

    /// <summary>
    ///     Where the screen came from: "official" (a real cabinet or PIUGAME) or the name of an
    ///     unofficial simulator (e.g. "stepp2"). Sims are kept for reference but are NOT part of
    ///     the training/accuracy target — we do not tune the extractor around their layouts. They
    ///     are not rejected either; an unverified user-submitted sim score simply is what it is.
    /// </summary>
    public string Source { get; init; } = "official";
    public string Song { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public string PrintedScore { get; init; } = "";
    public string Grade { get; init; } = "";
    public string Plate { get; init; } = "";
    public string Perfect { get; init; } = "";
    public string Great { get; init; } = "";
    public string Good { get; init; } = "";
    public string Bad { get; init; } = "";
    public string Miss { get; init; } = "";
    public string MaxCombo { get; init; } = "";
    public string Kcal { get; init; } = "";
}
