using System.Diagnostics;
using Tesseract;

namespace ScoreTracker.Tests.Exploration.ScoreImageExtraction;

/// <summary>
///     Iteration 1 of the authored-localization extractor, headless. Two passes:
///     pass 1 OCRs the whole photo to locate label word-boxes (labels are plain white text
///     and read far more reliably than the colored digits); pass 2 re-OCRs a tight strip at
///     each label's row with a digits-only whitelist. Broken is detected by the absence of a
///     plate word. Single-panel only — a 2-player photo produces one (wrong) merged panel,
///     which the scorer reports honestly.
/// </summary>
public sealed class PhoenixScreenExtractor : IDisposable
{
    private readonly TesseractEngine _anchorEngine;
    private readonly TesseractEngine _digitEngine;

    private static readonly (string Key, string[] Targets)[] JudgementAnchors =
    {
        ("perfect", new[] { "PERFECT" }),
        ("great", new[] { "GREAT" }),
        ("good", new[] { "GOOD" }),
        ("bad", new[] { "BAD" }),
        ("miss", new[] { "MISS" }),
        ("combo", new[] { "COMBO" }),
    };

    // Plate adjectives that mark a pass. PERFECT is deliberately absent (it collides with the
    // judgement label); a Perfect Game is caught by the GAME-word fallback in DetectPlate.
    private static readonly string[] PlateAdjectives =
        { "MARVELOUS", "SUPERB", "ULTIMATE", "EXTREME", "TALENTED", "FAIR", "ROUGH" };

    public PhoenixScreenExtractor(string tessdataPath)
    {
        _anchorEngine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
        _digitEngine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
        _digitEngine.SetVariable("tessedit_char_whitelist", "0123456789");
        _digitEngine.DefaultPageSegMode = PageSegMode.SingleLine;
    }

    public ExtractedPanel Extract(string imagePath)
    {
        var sw = Stopwatch.StartNew();
        using var loaded = Pix.LoadFromFile(imagePath);

        // Phone JPEGs carry orientation as an EXIF flag which Leptonica ignores, so the pixels
        // are often sideways. Hunt anchors at all four rotations on a small grayscale copy and
        // keep the rotation that yields the most judgement labels.
        var (pix, words, rotation) = FindBestOrientation(loaded);
        try
        {
            return ExtractFromOriented(pix, words, imagePath, rotation, sw);
        }
        finally
        {
            if (!ReferenceEquals(pix, loaded)) pix.Dispose();
        }
    }

    private (Pix pix, List<Word> words, int rotation) FindBestOrientation(Pix original)
    {
        Pix best = original;
        var bestWords = new List<Word>();
        var bestCount = -1;
        var bestRotation = 0;

        var current = original;
        var owned = new List<Pix>();
        for (var rotation = 0; rotation < 4; rotation++)
        {
            if (rotation > 0)
            {
                current = current.Rotate90(1);
                owned.Add(current);
            }

            var words = ReadWordsQuick(current);
            var count = JudgementAnchors.Count(a => FindAnchor(words, a.Targets) is not null)
                        + (FindAnchor(words, new[] { "SCORE" }) is null ? 0 : 1);
            if (count > bestCount)
            {
                bestCount = count;
                best = current;
                bestRotation = rotation;
            }

            if (count >= 5) break; // orientation is unambiguous
        }

        // Full ensemble pass on the winning orientation only.
        bestWords = ReadWordsEnsemble(best);

        foreach (var pix in owned.Where(p => !ReferenceEquals(p, best)))
            pix.Dispose();
        return (best, bestWords, bestRotation * 90);
    }

    private const int AnchorPassMaxDimension = 1800;

    /// <summary>Cheap single-variant pass for the orientation search.</summary>
    private List<Word> ReadWordsQuick(Pix pix)
    {
        var scale = Math.Min(1f, AnchorPassMaxDimension / (float)Math.Max(pix.Width, pix.Height));
        using var small = scale < 1f ? pix.Scale(scale, scale) : pix.Clone();
        using var gray = small.Depth == 32 ? small.ConvertRGBToGray() : small.Clone();
        return ReadWords(gray, 1f / scale);
    }

    /// <summary>
    ///     Pass 1: sparse-text OCR over complementary variants, merged. The variants see
    ///     different fields — downscaled-gray finds the judgement label stack, Sauvola
    ///     binarization lifts the stylised score digits, full-res color reads nameplates —
    ///     so the union is far more complete than any single pass. Word boxes are always
    ///     mapped back to full-res coordinates.
    /// </summary>
    private List<Word> ReadWordsEnsemble(Pix pix)
    {
        var words = new List<Word>();
        var scale = Math.Min(1f, AnchorPassMaxDimension / (float)Math.Max(pix.Width, pix.Height));

        using var small = scale < 1f ? pix.Scale(scale, scale) : pix.Clone();
        using var gray = small.Depth == 32 ? small.ConvertRGBToGray() : small.Clone();
        words.AddRange(ReadWords(gray, 1f / scale));

        using var binarized = gray.BinarizeSauvola(16, 0.35f, false);
        words.AddRange(ReadWords(binarized, 1f / scale));

        words.AddRange(ReadWords(pix, 1f));

        return words;
    }

    private ExtractedPanel ExtractFromOriented(Pix pix, List<Word> words, string imagePath, int rotation,
        Stopwatch sw)
    {
        var anchorsFound = new List<string>();
        if (rotation != 0) anchorsFound.Add($"rot{rotation}");

        var numbers = NumberWords(words);
        var values = new Dictionary<string, int>();
        var labelBoxes = new Dictionary<string, Rect>();
        foreach (var (key, targets) in JudgementAnchors)
        {
            var anchor = FindAnchor(words, targets);
            if (anchor is null)
            {
                values[key] = -1;
                continue;
            }

            anchorsFound.Add(key);
            labelBoxes[key] = anchor.Bounds;
            // Prefer a number pass 1 already read on this label's row; the sparse ensemble
            // reads isolated counts far better than a fresh full-width row crop does.
            values[key] = HarvestRowNumber(numbers, anchor.Bounds)
                          ?? ReadRowDigits(pix, anchor.Bounds);
        }

        InferMissingRows(labelBoxes, values, numbers, pix, anchorsFound);

        var score = HarvestScore(numbers) ?? ReadScore(pix, words, anchorsFound);
        var platePresent = DetectPlate(words);

        return new ExtractedPanel
        {
            Image = Path.GetFileName(imagePath),
            Score = score,
            Perfect = values["perfect"],
            Great = values["great"],
            Good = values["good"],
            Bad = values["bad"],
            Miss = values["miss"],
            MaxCombo = values["combo"],
            Broken = !platePresent,
            AnchorsFound = anchorsFound,
            ElapsedMs = sw.ElapsedMilliseconds,
        };
    }

    private sealed record Word(string Text, Rect Bounds, float Confidence);

    private sealed record NumberWord(long Value, int Digits, Rect Bounds, float Confidence);

    /// <summary>
    ///     Pass-1 words that are clean digit runs. Tokens with '.' (kcal), '+' or ',' (the
    ///     score-delta like +38,522) are excluded — they are real numbers but never the
    ///     score or a judgement count.
    /// </summary>
    private static List<NumberWord> NumberWords(List<Word> words)
    {
        var numbers = new List<NumberWord>();
        foreach (var word in words)
        {
            var text = word.Text.Trim();
            if (text.Length is 0 or > 8) continue;
            if (text.Contains('.') || text.Contains('+') || text.Contains(',')) continue;
            if (!text.All(char.IsDigit)) continue;
            numbers.Add(new NumberWord(long.Parse(text), text.Length, word.Bounds, word.Confidence));
        }

        return numbers;
    }

    /// <summary>The digit word vertically centred on the label's row, nearest horizontally.</summary>
    private static int? HarvestRowNumber(List<NumberWord> numbers, Rect label)
    {
        var rowCenter = (label.Y1 + label.Y2) / 2;
        var candidates = numbers
            .Where(n => n.Value <= 9999 && n.Digits <= 4)
            .Where(n => n.Bounds.Y1 <= rowCenter && rowCenter <= n.Bounds.Y2)
            .OrderBy(n => HorizontalGap(n.Bounds, label))
            .ToList();
        return candidates.Count == 0 ? null : (int)candidates[0].Value;
    }

    private static int HorizontalGap(Rect a, Rect b) =>
        a.X2 < b.X1 ? b.X1 - a.X2 : b.X2 < a.X1 ? a.X1 - b.X2 : 0;

    /// <summary>
    ///     The judgement labels are a fixed-order, evenly-spaced vertical stack
    ///     (PERFECT..MAX COMBO). When at least two labels anchored, fit y = a + b·index and
    ///     synthesise a label box for each row whose label OCR could not read (PERFECT and
    ///     MISS are the most saturated colors and fail most) — then harvest its number the
    ///     same way. Localization by geometry where recognition fails.
    /// </summary>
    private void InferMissingRows(Dictionary<string, Rect> labelBoxes, Dictionary<string, int> values,
        List<NumberWord> numbers, Pix pix, List<string> anchorsFound)
    {
        var stackIndex = new Dictionary<string, int>
            { ["perfect"] = 0, ["great"] = 1, ["good"] = 2, ["bad"] = 3, ["miss"] = 4, ["combo"] = 5 };
        var found = labelBoxes.Where(kv => stackIndex.ContainsKey(kv.Key))
            .Select(kv => (Index: stackIndex[kv.Key], Box: kv.Value))
            .ToList();
        if (found.Select(f => f.Index).Distinct().Count() < 2) return;

        // Least-squares fit of row center against stack index.
        var meanIdx = found.Average(f => f.Index);
        var meanY = found.Average(f => (f.Box.Y1 + f.Box.Y2) / 2.0);
        var pitch = found.Sum(f => (f.Index - meanIdx) * ((f.Box.Y1 + f.Box.Y2) / 2.0 - meanY))
                    / found.Sum(f => (f.Index - meanIdx) * (f.Index - meanIdx));
        if (pitch <= 0) return; // stack must run downward; anything else is a bad fit

        var height = (int)found.Average(f => f.Box.Height);
        var x1 = found.Min(f => f.Box.X1);
        var x2 = found.Max(f => f.Box.X2);

        foreach (var (key, index) in stackIndex)
        {
            if (values[key] >= 0) continue;
            var center = (int)(meanY + (index - meanIdx) * pitch);
            var y1 = Math.Max(0, center - height / 2);
            var y2 = Math.Min(pix.Height, center + height / 2);
            if (y2 <= y1) continue;
            var synthetic = Rect.FromCoords(x1, y1, x2, y2);

            var value = HarvestRowNumber(numbers, synthetic) ?? ReadRowDigits(pix, synthetic);
            if (value < 0) continue;
            values[key] = value;
            anchorsFound.Add(key + "~");
        }
    }

    /// <summary>
    ///     The printed score is a 6-7 digit run ≤ 1,000,000 (leading zero usually included).
    ///     Harvest the highest-confidence such run from pass 1.
    /// </summary>
    private static int? HarvestScore(List<NumberWord> numbers)
    {
        var best = numbers
            .Where(n => n.Digits is 6 or 7 && n.Value <= 1_000_000 && n.Value > 1000)
            .OrderByDescending(n => n.Digits == 7)
            .ThenByDescending(n => n.Confidence)
            .FirstOrDefault();
        return best is null ? null : (int)best.Value;
    }

    private List<Word> ReadWords(Pix pix, float scaleBack)
    {
        var words = new List<Word>();
        // SparseText: "find as much text as possible in no particular order" — PSM.Auto's
        // layout analysis proposes almost no regions on busy arcade photos.
        using var page = _anchorEngine.Process(pix, PageSegMode.SparseText);
        using var it = page.GetIterator();
        it.Begin();
        do
        {
            var text = it.GetText(PageIteratorLevel.Word);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (!it.TryGetBoundingBox(PageIteratorLevel.Word, out var bounds)) continue;
            var mapped = scaleBack == 1f
                ? bounds
                : Rect.FromCoords(
                    (int)(bounds.X1 * scaleBack), (int)(bounds.Y1 * scaleBack),
                    (int)(bounds.X2 * scaleBack), (int)(bounds.Y2 * scaleBack));
            words.Add(new Word(text.Trim(), mapped, it.GetConfidence(PageIteratorLevel.Word)));
        } while (it.Next(PageIteratorLevel.Word));

        return words;
    }

    private static string Normalize(string raw) =>
        new(raw.ToUpperInvariant().Where(char.IsAsciiLetterUpper).ToArray());

    private static Word? FindAnchor(List<Word> words, string[] targets)
    {
        Word? best = null;
        foreach (var word in words)
        {
            var text = Normalize(word.Text);
            if (text.Length < 3) continue;
            foreach (var target in targets)
                if (Matches(text, target) && (best is null || word.Confidence > best.Confidence))
                    best = word;
        }

        return best;
    }

    private static bool Matches(string text, string target) =>
        text == target
        || (text.Length >= 4 && target.Contains(text))
        || (target.Length >= 4 && text.Contains(target))
        || LevenshteinAtMostOne(text, target);

    private static bool LevenshteinAtMostOne(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return false;
        // One pass: find first mismatch, then compare the remainders under each edit type.
        int i = 0, j = 0, edits = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j])
            {
                i++;
                j++;
                continue;
            }

            if (++edits > 1) return false;
            if (a.Length > b.Length) i++;
            else if (b.Length > a.Length) j++;
            else
            {
                i++;
                j++;
            }
        }

        return edits + (a.Length - i) + (b.Length - j) <= 1;
    }

    /// <summary>Crop the label's row full-width, then read it digits-only.</summary>
    private int ReadRowDigits(Pix pix, Rect label)
    {
        var pad = (int)(label.Height * 0.35);
        var y1 = Math.Max(0, label.Y1 - pad);
        var y2 = Math.Min(pix.Height, label.Y2 + pad);
        var region = Rect.FromCoords(0, y1, pix.Width, y2);

        var runs = DigitRuns(region, pix);
        // The judgement count is a 1-4 digit number; take the longest plausible run.
        var bestRun = runs.Where(r => r.Length is >= 1 and <= 4).OrderByDescending(r => r.Length)
            .FirstOrDefault();
        return bestRun is null ? -1 : int.Parse(bestRun);
    }

    /// <summary>The score digits sit below the SCORE label; crop a box under it and read.</summary>
    private int ReadScore(Pix pix, List<Word> words, List<string> anchorsFound)
    {
        var anchor = FindAnchor(words, new[] { "SCORE" });
        if (anchor is null) return -1;
        anchorsFound.Add("score");

        var label = anchor.Bounds;
        var xPad = label.Width * 3;
        var region = Rect.FromCoords(
            Math.Max(0, label.X1 - xPad),
            label.Y2,
            Math.Min(pix.Width, label.X2 + xPad),
            Math.Min(pix.Height, label.Y2 + label.Height * 4));

        var runs = DigitRuns(region, pix);
        // Prefer a single 6-8 digit run (the printed score has a leading zero); fall back to
        // concatenating everything when OCR split the digits apart.
        var single = runs.Where(r => r.Length is >= 6 and <= 8)
            .Select(r => long.Parse(r))
            .FirstOrDefault(v => v <= 1_000_000);
        if (single > 0) return (int)single;

        var joined = string.Concat(runs);
        if (joined.Length is >= 6 and <= 8 && long.Parse(joined) <= 1_000_000)
            return (int)long.Parse(joined);

        return -1;
    }

    private List<string> DigitRuns(Rect region, Pix pix)
    {
        if (region.Width < 10 || region.Height < 5) return new List<string>();
        using var page = _digitEngine.Process(pix, region, PageSegMode.SingleLine);
        var text = page.GetText() ?? "";
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(token => SplitRuns(token))
            .ToList();
    }

    private static IEnumerable<string> SplitRuns(string token)
    {
        var run = "";
        foreach (var c in token)
            if (char.IsDigit(c))
            {
                run += c;
            }
            else if (run.Length > 0)
            {
                yield return run;
                run = "";
            }

        if (run.Length > 0) yield return run;
    }

    /// <summary>
    ///     Pass/break tell: passes always print a plate word, breaks never do. Look for a plate
    ///     adjective anywhere; a Perfect Game (adjective collides with the judgement label) is
    ///     caught by a GAME word whose upstairs neighbour is PERFECT.
    /// </summary>
    private static bool DetectPlate(List<Word> words)
    {
        foreach (var word in words)
        {
            var text = Normalize(word.Text);
            if (text.Length < 4) continue;
            if (PlateAdjectives.Any(adj => Matches(text, adj))) return true;
        }

        foreach (var game in words.Where(w => Normalize(w.Text) == "GAME"))
        {
            var above = words.FirstOrDefault(w =>
                Normalize(w.Text) == "PERFECT"
                && w.Bounds.Y2 <= game.Bounds.Y1
                && game.Bounds.Y1 - w.Bounds.Y2 < game.Bounds.Height * 2
                && w.Bounds.X1 < game.Bounds.X2 && game.Bounds.X1 < w.Bounds.X2);
            if (above is not null) return true;
        }

        return false;
    }

    public void Dispose()
    {
        _anchorEngine.Dispose();
        _digitEngine.Dispose();
    }
}
