using System;
using System.Globalization;
using System.Linq;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Tests.ScoreImageExtraction;

/// <summary>
///     Turns a raw image read into a checked score by cross-validating the fields against each
///     other. The Phoenix score is a deterministic function of the five judgements plus max
///     combo, so recomputing it from the read judgements and comparing to the read score is an
///     over-determined checksum: a clean match is high confidence, a mismatch localises the
///     misread. This reuses the shipped <see cref="ScoreScreen" /> so the PoC exercises the real
///     scoring math, not a copy of it.
/// </summary>
public static class ScoreReconciler
{
    // A judged score whose recompute lands this far from the printed score (or closer) is
    // treated as a confident read; beyond it, the record is flagged for human confirmation.
    private const int ToleranceInPoints = 2;

    public static ReconciliationResult Reconcile(RawScoreRead read)
    {
        var perfect = ParseInt(read.Perfect);
        var great = ParseInt(read.Great);
        var good = ParseInt(read.Good);
        var bad = ParseInt(read.Bad);
        var miss = ParseInt(read.Miss);
        var maxCombo = ParseInt(read.MaxCombo);
        var kcal = ParseDouble(read.Kcal);
        var printed = ParseInt(read.PrintedScore);

        var noteCount = perfect + great + good + bad + miss;

        var screen = new ScoreScreen(perfect, great, good, bad, miss, maxCombo, kcal);

        // The shipped formula ceils; the raw (pre-rounding) value lets us also test flooring,
        // which is what the real cabinets turn out to print.
        var exact = ExactScore(perfect, great, good, bad, miss, maxCombo, noteCount);
        var floorScore = noteCount == 0 ? 0 : (int)Math.Floor(exact);
        int ceilScore = screen.CalculatePhoenixScore; // == Math.Ceiling(exact)

        var delta = printed - floorScore;
        var status = Math.Abs(delta) switch
        {
            0 => ReconciliationStatus.Exact,
            <= ToleranceInPoints => ReconciliationStatus.Close,
            _ => ReconciliationStatus.Flagged
        };

        var roundingMatch = ceilScore == printed ? "ceil"
            : floorScore == printed ? "floor"
            : "neither";

        var derivedGrade = ((PhoenixScore)Clamp(printed)).LetterGrade.GetName();
        var derivedPlate = screen.PlateText.GetName();

        var confidence = status switch
        {
            ReconciliationStatus.Exact => 0.99,
            ReconciliationStatus.Close => 0.80,
            _ => 0.30
        };

        return new ReconciliationResult(
            read.Image, read.Source, read.Song, read.Difficulty,
            printed, floorScore, ceilScore, delta,
            roundingMatch, status,
            derivedGrade, read.Grade,
            derivedPlate, read.Plate,
            noteCount, maxCombo, confidence);
    }

    private static double ExactScore(int perfect, int great, int good, int bad, int miss, int maxCombo,
        int noteCount)
    {
        if (noteCount == 0) return 0;
        return (0.995 * (1.0 * perfect + 0.6 * great + 0.2 * good + 0.1 * bad) + 0.005 * maxCombo)
            / noteCount * 1000000.0;
    }

    private static int Clamp(int score) => Math.Clamp(score, 0, 1000000);

    private static int ParseInt(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? 0 : int.Parse(digits, CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string raw)
    {
        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}

public enum ReconciliationStatus
{
    Exact,
    Close,
    Flagged
}

public sealed record ReconciliationResult(
    string Image,
    string Source,
    string Song,
    string Difficulty,
    int PrintedScore,
    int FloorScore,
    int CeilScore,
    int Delta,
    string RoundingMatch,
    ReconciliationStatus Status,
    string DerivedGrade,
    string ReadGrade,
    string DerivedPlate,
    string ReadPlate,
    int NoteCount,
    int MaxCombo,
    double Confidence);
