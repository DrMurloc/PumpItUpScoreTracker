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

    /// <summary>Player nameplate — the attribution key, matched to an account downstream.</summary>
    public string Gametag { get; init; } = "";

    /// <summary>Which side the panel sits on ("1P"/"2P"), or "" for a lone single-player panel.</summary>
    public string Player { get; init; } = "";

    /// <summary>
    ///     Whether the stage was broken (failed). MUST be read, never derived — a play can end with
    ///     clean judgements yet have broken mid-song. The on-screen tell is the ABSENCE of a plate
    ///     word (Marvelous/Superb/.../Rough Game): passes always show one, breaks never do. Grade
    ///     colour is an unreliable proxy because AA/AA+ are naturally silver like a broken grade.
    /// </summary>
    public bool Broken { get; init; }

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
