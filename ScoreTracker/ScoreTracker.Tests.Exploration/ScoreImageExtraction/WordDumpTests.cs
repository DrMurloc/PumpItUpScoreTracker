using Tesseract;
using Xunit.Abstractions;

namespace ScoreTracker.Tests.Exploration.ScoreImageExtraction;

/// <summary>
///     Diagnostic: dump every word pass-1 OCR sees for one image, per rotation and per
///     preprocessing variant. Run by hand when the anchor pass is missing labels to learn
///     whether the failure is orientation, scale, or recognition.
/// </summary>
public sealed class WordDumpTests
{
    private readonly ITestOutputHelper _output;

    public WordDumpTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("img16.jpg")]
    [InlineData("img03.jpg")]
    public void Dump_pass1_words_per_variant(string image)
    {
        if (!ExplorationPaths.InputsAvailable(out var reason))
        {
            _output.WriteLine($"SKIPPED — {reason}");
            return;
        }

        using var engine = new TesseractEngine(ExplorationPaths.TessdataDirectory, "eng", EngineMode.Default);
        using var original = Pix.LoadFromFile(Path.Combine(ExplorationPaths.ImagesDirectory, image));
        _output.WriteLine($"{image}: {original.Width}x{original.Height} depth={original.Depth}");

        DumpVariant(engine, original, "auto full-res", PageSegMode.Auto);
        DumpVariant(engine, original, "sparse full-res", PageSegMode.SparseText);

        var scale = Math.Min(1f, 1800f / Math.Max(original.Width, original.Height));
        using var small = original.Scale(scale, scale);
        DumpVariant(engine, small, "sparse downscaled-color", PageSegMode.SparseText);

        using var gray = small.ConvertRGBToGray();
        DumpVariant(engine, gray, "sparse downscaled-gray", PageSegMode.SparseText);

        using var binarized = gray.BinarizeSauvola(16, 0.35f, false);
        DumpVariant(engine, binarized, "sparse downscaled-sauvola", PageSegMode.SparseText);
    }

    private void DumpVariant(TesseractEngine engine, Pix pix, string label, PageSegMode psm)
    {
        using var page = engine.Process(pix, psm);
        var words = new List<string>();
        using var it = page.GetIterator();
        it.Begin();
        do
        {
            var text = it.GetText(PageIteratorLevel.Word)?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var conf = it.GetConfidence(PageIteratorLevel.Word);
            if (conf > 40 && text.Length >= 3) words.Add($"{text}({conf:F0})");
        } while (it.Next(PageIteratorLevel.Word));

        _output.WriteLine($"--- {label}: {words.Count} words ---");
        _output.WriteLine(string.Join(" ", words.Take(60)));
    }
}
