using Microsoft.AspNetCore.Components.Forms;
using ScoreTracker.Domain.Records;
using ScoreTracker.Web.Services.Contracts;
using Tesseract;

namespace ScoreTracker.Web.Services;

public sealed class OcrScoreImageExtractor : IScoreImageExtractor
{
    public async Task<ScoreScreen> GetScore(IBrowserFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream(cancellationToken: cancellationToken);

        using var ms = new MemoryStream();

        await stream.CopyToAsync(ms, cancellationToken);
        var fileBytes = ms.ToArray();

        using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
        engine.DefaultPageSegMode = PageSegMode.SparseText;

        using var img = Pix.LoadFromMemory(fileBytes);

        using var page = engine.Process(img);

        var text = page.GetText();
        return new ScoreScreen(0, 0, 0, 0, 0, 0);
    }
}