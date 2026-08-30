using System.Collections.Concurrent;
using QRCoder;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using SkiaSharp;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     SkiaSharp share-card renderer (tier-lists overhaul C8): cross-platform successor
///     to the page-side System.Drawing stitch. Theme-blind — every color arrives as a
///     hex the caller resolved from the mix palette. One renderer, two consumers: the
///     Download button and the per-folder og:image job.
/// </summary>
public sealed class SkiaShareCardRenderer : IShareCardRenderer
{
    private const int Width = 1000;
    private const int Pad = 16;
    private const int TileWidth = 140;
    private const int JacketHeight = 80;
    private const int BandHeight = 24;
    private const int RowLabelHeight = 38;
    private const int HeaderHeight = 96;
    private const int FooterHeight = 96;
    private const int GradeWidth = 34;
    private const int GradeHeight = 24;
    private const int BubbleHeight = 24;
    private const int QrSize = 64;
    private const int LegendRowHeight = 24;

    /// <summary>
    ///     How many images fetch at once — for the pre-load and the page-driven prefetch both.
    ///     Bounded (design doc §8): an unbounded WhenAll over sixty jackets was a burst the
    ///     image host and this process's memory ramp both felt.
    /// </summary>
    private const int FetchBatchSize = 8;

    private static readonly HttpClient Http = new();
    private static readonly ConcurrentDictionary<string, SKBitmap?> ImageCache = new();

    public async Task PrefetchImages(IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        foreach (var batch in urls.Distinct().Chunk(FetchBatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAll(batch.Select(u => LoadImage(u, cancellationToken)));
        }
    }

    public async Task<byte[]> RenderTierListCard(TierListShareCard card,
        CancellationToken cancellationToken = default)
    {
        // A folder is ~60 tiles; fetching each jacket/grade/plate sequentially made a
        // cold render take ~10s. All the URLs are independent, so fetch them in bounded
        // batches — one pre-load pass de-duped by URL, then the per-tile lookups hit the
        // cache. A page that already prefetched pays nothing here.
        var allUrls = card.Rows.SelectMany(r => r.Tiles)
            .SelectMany(t => new[] { t.JacketUrl, t.GradeUrl, t.PlateUrl, t.BubbleUrl, t.ExpectedGradeUrl })
            .Append(card.BubbleUrl)
            .Where(u => u != null)
            .Select(u => u!)
            .Distinct()
            .ToArray();
        await PrefetchImages(allUrls, cancellationToken);

        var bubble = card.BubbleUrl == null ? null : await LoadImage(card.BubbleUrl, cancellationToken);
        var tiles = new Dictionary<TierListShareCard.Tile,
            (SKBitmap? Jacket, SKBitmap? Grade, SKBitmap? Plate, SKBitmap? Bubble, SKBitmap? Expected)>();
        foreach (var tile in card.Rows.SelectMany(r => r.Tiles))
            tiles[tile] = (await LoadImage(tile.JacketUrl, cancellationToken),
                tile.GradeUrl == null ? null : await LoadImage(tile.GradeUrl, cancellationToken),
                tile.PlateUrl == null ? null : await LoadImage(tile.PlateUrl, cancellationToken),
                tile.BubbleUrl == null ? null : await LoadImage(tile.BubbleUrl, cancellationToken),
                tile.ExpectedGradeUrl == null ? null : await LoadImage(tile.ExpectedGradeUrl, cancellationToken));

        // The bands are card-uniform: one raggedly taller tile makes a grid unreadable, so if
        // any tile carries a caption (or chips), every tile pays that band's height.
        var all = card.Rows.SelectMany(r => r.Tiles).ToArray();
        var captionBand = all.Any(t => t.Caption != null);
        var skillsBand = all.Any(t => t.SkillChips is { Count: > 0 });
        var tileHeight = JacketHeight + (captionBand ? BandHeight : 0) + (skillsBand ? BandHeight : 0);

        using var titlePaint = TextPaint(card.InkHex, 32, true);
        using var subtitlePaint = TextPaint(card.InkMutedHex, 15, false);
        using var stampPaint = TextPaint(card.AccentHex, 16, true);
        using var legendPaint = TextPaint(card.InkMutedHex, 13, false);

        var tilesPerRow = (Width - Pad) / (TileWidth + Pad);
        var legendHeight = MeasureLegendHeight(card.Legend, legendPaint);
        var height = HeaderHeight + FooterHeight + legendHeight + card.Rows.Sum(row =>
            RowLabelHeight + (int)Math.Ceiling((double)row.Tiles.Count / tilesPerRow) * (tileHeight + Pad));

        using var surface = SKSurface.Create(new SKImageInfo(Width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColor.Parse(card.BackgroundHex));

        // Header: bubble, title/subtitle, stamp box on the right.
        var y = Pad;
        var x = Pad;
        if (bubble != null)
        {
            canvas.DrawBitmap(bubble, SKRect.Create(x, y, 56, 56));
            x += 56 + Pad;
        }

        canvas.DrawText(card.Title, x, y + 32, titlePaint);
        canvas.DrawText(card.Subtitle, x, y + 58, subtitlePaint);

        var stampWidth = stampPaint.MeasureText(card.Stamp) + 24;
        var stampRect = SKRect.Create(Width - Pad - stampWidth, y + 8, stampWidth, 34);
        using (var stampBorder = new SKPaint
               {
                   Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColor.Parse(card.AccentHex),
                   IsAntialias = true
               })
        {
            canvas.DrawRoundRect(stampRect, 8, 8, stampBorder);
        }

        canvas.DrawText(card.Stamp, stampRect.Left + 12, stampRect.MidY + 6, stampPaint);
        y = HeaderHeight;

        // Tier rows: colored label, jacket tiles with their marks, bands and boundary.
        foreach (var row in card.Rows)
        {
            using var rowPaint = TextPaint(row.ColorHex, 20, true);
            canvas.DrawText(row.Name, Pad, y + 24, rowPaint);
            y += RowLabelHeight;
            x = Pad;
            foreach (var tile in row.Tiles)
            {
                if (x + TileWidth > Width - Pad)
                {
                    x = Pad;
                    y += tileHeight + Pad;
                }

                DrawTile(canvas, card, tile, tiles[tile], SKRect.Create(x, y, TileWidth, tileHeight),
                    captionBand, skillsBand);
                x += TileWidth + Pad;
            }

            y += tileHeight + Pad;
        }

        y += DrawLegend(canvas, card.Legend, legendPaint, y);

        // Footer: canonical link + QR to the live list.
        using (var line = new SKPaint
               {
                   Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = SKColor.Parse(card.InkMutedHex)
               })
        {
            canvas.DrawLine(Pad, y + 4, Width - Pad, y + 4, line);
        }

        canvas.DrawText(card.LinkUrl, Pad, y + 40, subtitlePaint);
        var qr = RenderQr(card.LinkUrl);
        canvas.DrawBitmap(qr, SKRect.Create(Width - Pad - QrSize, y + 12, QrSize, QrSize));

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        return data.ToArray();
    }

    private static void DrawTile(SKCanvas canvas, TierListShareCard card, TierListShareCard.Tile tile,
        (SKBitmap? Jacket, SKBitmap? Grade, SKBitmap? Plate, SKBitmap? Bubble, SKBitmap? Expected) art,
        SKRect rect, bool captionBand, bool skillsBand)
    {
        var jacketRect = SKRect.Create(rect.Left, rect.Top, rect.Width, JacketHeight);
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, 6));
        if (captionBand || skillsBand)
            using (var bandFill = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColor.Parse(card.SurfaceHex) })
            {
                canvas.DrawRect(SKRect.Create(rect.Left, jacketRect.Bottom, rect.Width, rect.Bottom - jacketRect.Bottom),
                    bandFill);
            }

        if (art.Jacket != null) canvas.DrawBitmap(art.Jacket, jacketRect);

        var bandY = jacketRect.Bottom;
        if (captionBand)
        {
            if (tile.Caption != null)
                using (var captionPaint = TextPaint(card.InkHex, 12, true))
                {
                    canvas.DrawText(Ellipsize(tile.Caption, captionPaint, rect.Width - 14),
                        rect.Left + 7, bandY + 16, captionPaint);
                }

            bandY += BandHeight;
        }

        if (skillsBand) DrawSkillChips(canvas, tile.SkillChips, rect, bandY);
        canvas.Restore();

        // The top-left, where the page's card wears it. Width comes off the art rather
        // than a constant: the bubble sets are wider than they are tall, and a co-op
        // bubble is wider still, so a square box would squash whichever it wasn't cut for.
        if (art.Bubble != null)
        {
            var bubbleWidth = art.Bubble.Height == 0
                ? BubbleHeight
                : BubbleHeight * (float)art.Bubble.Width / art.Bubble.Height;
            canvas.DrawBitmap(art.Bubble,
                SKRect.Create(jacketRect.Left + 4, jacketRect.Top + 4, bubbleWidth, BubbleHeight));
        }

        if (tile.CompactMarks) DrawCompactMarks(canvas, card, tile, art, jacketRect);
        else DrawClassicMarks(canvas, card, tile, art, jacketRect);

        if (tile.BadgeHex != null)
        {
            if (tile.Outline == TileOutline.Dot)
                using (var badge = new SKPaint
                       {
                           Style = SKPaintStyle.Fill, Color = SKColor.Parse(tile.BadgeHex), IsAntialias = true
                       })
                {
                    canvas.DrawCircle(rect.Right - 9, rect.Top + 9, 6, badge);
                }
            else
            {
                var borderRect = new SKRoundRect(SKRect.Inflate(rect, -1, -1), 6);
                // The Perfect Game's halo: a blurred pass under the crisp border, so the
                // glow reads as light rather than as a thicker line.
                if (tile.Glow)
                    using (var halo = new SKPaint
                           {
                               Style = SKPaintStyle.Stroke, StrokeWidth = 5,
                               Color = SKColor.Parse(tile.BadgeHex).WithAlpha(150), IsAntialias = true,
                               MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
                           })
                    {
                        canvas.DrawRoundRect(borderRect, halo);
                    }

                using (var border = new SKPaint
                       {
                           Style = SKPaintStyle.Stroke, StrokeWidth = 2,
                           Color = SKColor.Parse(tile.BadgeHex), IsAntialias = true,
                           PathEffect = DashFor(tile.Outline)
                       })
                {
                    canvas.DrawRoundRect(borderRect, border);
                }
            }
        }
    }

    /// <summary>
    ///     The original bottom edge, right to left, exactly as the Compact card stacked it before
    ///     download settings existed: a printed corner value wins the right, the grade steps to
    ///     the LEFT corner when there is one, and the plate takes what is left in between.
    /// </summary>
    private static void DrawClassicMarks(SKCanvas canvas, TierListShareCard card, TierListShareCard.Tile tile,
        (SKBitmap? Jacket, SKBitmap? Grade, SKBitmap? Plate, SKBitmap? Bubble, SKBitmap? Expected) art,
        SKRect jacketRect)
    {
        var right = jacketRect.Right - 4;
        if (tile.CornerLabel != null)
            right = DrawCorner(canvas, tile.CornerLabel, tile.CornerHex ?? card.AccentHex, right,
                jacketRect.Bottom - 4, card.InkHex);

        if (art.Plate != null)
        {
            canvas.DrawBitmap(art.Plate,
                SKRect.Create(right - GradeWidth, jacketRect.Bottom - GradeHeight - 4, GradeWidth, GradeHeight));
            right -= GradeWidth + 4;
        }

        if (art.Grade != null)
            canvas.DrawBitmap(art.Grade, tile.CornerLabel == null
                ? SKRect.Create(right - GradeWidth, jacketRect.Bottom - GradeHeight - 4, GradeWidth, GradeHeight)
                : SKRect.Create(jacketRect.Left + 4, jacketRect.Bottom - GradeHeight - 4, GradeWidth, GradeHeight));
    }

    /// <summary>
    ///     The settings-era strip (design doc §2): the score sits leftmost as plain text, the
    ///     grade and plate stack beside it at the score's own height, and the right edge belongs
    ///     to the corner chip with the expected-grade art riding just inside it.
    /// </summary>
    private static void DrawCompactMarks(SKCanvas canvas, TierListShareCard card, TierListShareCard.Tile tile,
        (SKBitmap? Jacket, SKBitmap? Grade, SKBitmap? Plate, SKBitmap? Bubble, SKBitmap? Expected) art,
        SKRect jacketRect)
    {
        var left = jacketRect.Left + 4;
        if (tile.ScoreLabel != null)
        {
            using var scorePaint = TextPaint(tile.ScoreMuted ? card.InkMutedHex : "#FFFFFF", 13, true);
            using var shadow = TextPaint("#000000", 13, true);
            canvas.DrawText(tile.ScoreLabel, left + 1, jacketRect.Bottom - 5, shadow);
            canvas.DrawText(tile.ScoreLabel, left, jacketRect.Bottom - 6, scorePaint);
            left += scorePaint.MeasureText(tile.ScoreLabel) + 5;
        }

        // The stack: grade over plate, each sized to the score text's height, one unit.
        const int stackArtHeight = 14;
        if (art.Grade != null && art.Plate != null)
        {
            canvas.DrawBitmap(art.Grade, ArtRect(art.Grade, left, jacketRect.Bottom - 5 - 2 * stackArtHeight - 2,
                stackArtHeight));
            canvas.DrawBitmap(art.Plate, ArtRect(art.Plate, left, jacketRect.Bottom - 5 - stackArtHeight,
                stackArtHeight));
        }
        else if (art.Grade != null)
        {
            canvas.DrawBitmap(art.Grade, ArtRect(art.Grade, left, jacketRect.Bottom - 5 - stackArtHeight,
                stackArtHeight));
        }
        else if (art.Plate != null)
        {
            canvas.DrawBitmap(art.Plate, ArtRect(art.Plate, left, jacketRect.Bottom - 5 - stackArtHeight,
                stackArtHeight));
        }

        var right = jacketRect.Right - 4;
        if (tile.CornerLabel != null)
            right = DrawCorner(canvas, tile.CornerLabel, tile.CornerHex ?? card.AccentHex, right,
                jacketRect.Bottom - 4, card.InkHex);

        if (art.Expected != null)
        {
            const int expectedHeight = 16;
            var expectedRect = ArtRect(art.Expected, 0, jacketRect.Bottom - 5 - expectedHeight, expectedHeight);
            canvas.DrawBitmap(art.Expected,
                SKRect.Create(right - expectedRect.Width, expectedRect.Top, expectedRect.Width, expectedHeight));
        }
    }

    /// <summary>A bitmap's draw rect at a fixed height, width following the art's own aspect.</summary>
    private static SKRect ArtRect(SKBitmap art, float left, float top, float height)
    {
        var width = art.Height == 0 ? height : height * art.Width / art.Height;
        return SKRect.Create(left, top, width, height);
    }

    private static void DrawSkillChips(SKCanvas canvas, IReadOnlyList<TierListShareCard.SkillChip>? chips,
        SKRect rect, float bandY)
    {
        if (chips is not { Count: > 0 }) return;
        var x = rect.Left + 5;
        foreach (var chip in chips)
        {
            using var chipText = TextPaint(chip.Hex, 10, true);
            var width = chipText.MeasureText(chip.Label) + 12;
            if (x + width > rect.Right - 5) break;
            var box = SKRect.Create(x, bandY + 4, width, 16);
            using (var fill = new SKPaint
                   {
                       Style = SKPaintStyle.Fill, Color = SKColor.Parse(chip.Hex).WithAlpha(40), IsAntialias = true
                   })
            {
                canvas.DrawRoundRect(new SKRoundRect(box, 8), fill);
            }

            using (var border = new SKPaint
                   {
                       Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = SKColor.Parse(chip.Hex).WithAlpha(140),
                       IsAntialias = true
                   })
            {
                canvas.DrawRoundRect(new SKRoundRect(box, 8), border);
            }

            canvas.DrawText(chip.Label, box.Left + 6, box.Bottom - 4.5f, chipText);
            x += width + 4;
        }
    }

    /// <summary>How the legend flows at the card's width, so the surface is tall enough before it exists.</summary>
    private static int MeasureLegendHeight(IReadOnlyList<TierListShareCard.LegendEntry>? legend, SKPaint paint)
    {
        if (legend is not { Count: > 0 }) return 0;
        var rows = 1;
        var x = (float)Pad;
        foreach (var entry in legend)
        {
            var width = EntryWidth(entry, paint);
            if (x + width > Width - Pad)
            {
                rows++;
                x = Pad;
            }

            x += width;
        }

        return rows * LegendRowHeight + 4;
    }

    private static float EntryWidth(TierListShareCard.LegendEntry entry, SKPaint paint) =>
        14 + 6 + paint.MeasureText(entry.Label) + 20;

    /// <summary>The boundaries actually in the picture, each as its swatch and its words.</summary>
    private static int DrawLegend(SKCanvas canvas, IReadOnlyList<TierListShareCard.LegendEntry>? legend,
        SKPaint paint, int top)
    {
        if (legend is not { Count: > 0 }) return 0;
        var x = (float)Pad;
        var y = top + 4;
        foreach (var entry in legend)
        {
            var width = EntryWidth(entry, paint);
            if (x + width > Width - Pad)
            {
                y += LegendRowHeight;
                x = Pad;
            }

            using (var swatch = new SKPaint
                   {
                       Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColor.Parse(entry.Hex),
                       IsAntialias = true, PathEffect = DashFor(entry.Outline)
                   })
            {
                canvas.DrawRoundRect(new SKRoundRect(SKRect.Create(x, y + 4, 14, 14), 4), swatch);
            }

            canvas.DrawText(entry.Label, x + 20, y + 15, paint);
            x += width;
        }

        return y - top + LegendRowHeight;
    }

    private static string Ellipsize(string text, SKPaint paint, float maxWidth)
    {
        if (paint.MeasureText(text) <= maxWidth) return text;
        var kept = text;
        while (kept.Length > 1 && paint.MeasureText(kept + "…") > maxWidth) kept = kept[..^1];
        return kept + "…";
    }

    /// <summary>
    ///     The printed corner: the Compact card's chip — a near-black plate, a hairline border in
    ///     the value's own colour, the value in it — drawn right-aligned at <paramref name="right" />.
    ///     Returns the x its left edge took, so the next mark can stack beside it.
    /// </summary>
    private static float DrawCorner(SKCanvas canvas, string label, string hex, float right, float bottom,
        string inkHex)
    {
        using var text = TextPaint(hex, 13, true);
        var width = text.MeasureText(label) + 10;
        var box = SKRect.Create(right - width, bottom - 19, width, 19);
        using (var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(0, 0, 0, 209), IsAntialias = true })
        {
            canvas.DrawRoundRect(new SKRoundRect(box, 4), fill);
        }

        using (var border = new SKPaint
               {
                   Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = SKColor.Parse(hex), IsAntialias = true
               })
        {
            canvas.DrawRoundRect(new SKRoundRect(box, 4), border);
        }

        canvas.DrawText(label, box.Left + 5, box.Bottom - 5, text);
        return box.Left - 4;
    }

    private static SKPathEffect? DashFor(TileOutline outline) => outline switch
    {
        TileOutline.Dashed => SKPathEffect.CreateDash(new[] { 6f, 4f }, 0),
        TileOutline.Dotted => SKPathEffect.CreateDash(new[] { 2f, 3f }, 0),
        _ => null
    };

    private static SKPaint TextPaint(string hex, float size, bool bold)
    {
        return new SKPaint
        {
            Color = SKColor.Parse(hex),
            TextSize = size,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial",
                bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        };
    }

    private static SKBitmap RenderQr(string url)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(4);
        return SKBitmap.Decode(png);
    }

    private static async Task<SKBitmap?> LoadImage(string url, CancellationToken cancellationToken)
    {
        if (ImageCache.TryGetValue(url, out var cached)) return cached;
        try
        {
            var bytes = await Http.GetByteArrayAsync(url, cancellationToken);
            var bitmap = SKBitmap.Decode(bytes);
            ImageCache[url] = bitmap;
            return bitmap;
        }
        catch (HttpRequestException)
        {
            // A missing jacket or badge shouldn't sink the whole card.
            ImageCache[url] = null;
            return null;
        }
    }
}
