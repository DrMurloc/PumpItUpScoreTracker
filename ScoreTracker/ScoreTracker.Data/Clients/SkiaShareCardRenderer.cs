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
    private const int TileHeight = 80;
    private const int RowLabelHeight = 38;
    private const int HeaderHeight = 96;
    private const int FooterHeight = 96;
    private const int GradeWidth = 34;
    private const int GradeHeight = 24;
    private const int BubbleHeight = 24;
    private const int QrSize = 64;

    private static readonly HttpClient Http = new();
    private static readonly ConcurrentDictionary<string, SKBitmap?> ImageCache = new();

    public async Task<byte[]> RenderTierListCard(TierListShareCard card,
        CancellationToken cancellationToken = default)
    {
        // A folder is ~60 tiles; fetching each jacket/grade/plate sequentially made a
        // cold render take ~10s. All the URLs are independent, so fetch them together —
        // one pre-load pass de-duped by URL, then the per-tile lookups hit the cache.
        var allUrls = card.Rows.SelectMany(r => r.Tiles)
            .SelectMany(t => new[] { t.JacketUrl, t.GradeUrl, t.PlateUrl, t.BubbleUrl })
            .Append(card.BubbleUrl)
            .Where(u => u != null)
            .Select(u => u!)
            .Distinct();
        await Task.WhenAll(allUrls.Select(u => LoadImage(u, cancellationToken)));

        var bubble = card.BubbleUrl == null ? null : await LoadImage(card.BubbleUrl, cancellationToken);
        var tiles = new Dictionary<TierListShareCard.Tile,
            (SKBitmap? Jacket, SKBitmap? Grade, SKBitmap? Plate, SKBitmap? Bubble)>();
        foreach (var tile in card.Rows.SelectMany(r => r.Tiles))
            tiles[tile] = (await LoadImage(tile.JacketUrl, cancellationToken),
                tile.GradeUrl == null ? null : await LoadImage(tile.GradeUrl, cancellationToken),
                tile.PlateUrl == null ? null : await LoadImage(tile.PlateUrl, cancellationToken),
                tile.BubbleUrl == null ? null : await LoadImage(tile.BubbleUrl, cancellationToken));

        var tilesPerRow = (Width - Pad) / (TileWidth + Pad);
        var height = HeaderHeight + FooterHeight + card.Rows.Sum(row =>
            RowLabelHeight + (int)Math.Ceiling((double)row.Tiles.Count / tilesPerRow) * (TileHeight + Pad));

        using var surface = SKSurface.Create(new SKImageInfo(Width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColor.Parse(card.BackgroundHex));

        using var titlePaint = TextPaint(card.InkHex, 32, true);
        using var subtitlePaint = TextPaint(card.InkMutedHex, 15, false);
        using var stampPaint = TextPaint(card.AccentHex, 16, true);
        using var labelPaint = TextPaint(card.InkHex, 20, true);

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

        // Tier rows: colored label, jacket tiles with grade/plate art and the badge dot.
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
                    y += TileHeight + Pad;
                }

                var rect = SKRect.Create(x, y, TileWidth, TileHeight);
                var (jacket, grade, plate, tileBubble) = tiles[tile];
                if (jacket != null)
                {
                    canvas.Save();
                    canvas.ClipRoundRect(new SKRoundRect(rect, 6));
                    canvas.DrawBitmap(jacket, rect);
                    canvas.Restore();
                }

                // The top-left, where the page's card wears it. Width comes off the art rather
                // than a constant: the bubble sets are wider than they are tall, and a co-op
                // bubble is wider still, so a square box would squash whichever it wasn't cut for.
                if (tileBubble != null)
                {
                    var bubbleWidth = tileBubble.Height == 0
                        ? BubbleHeight
                        : BubbleHeight * (float)tileBubble.Width / tileBubble.Height;
                    canvas.DrawBitmap(tileBubble,
                        SKRect.Create(rect.Left + 4, rect.Top + 4, bubbleWidth, BubbleHeight));
                }

                // The bottom edge, right to left, exactly as the Compact card stacks it: a printed
                // corner value wins the right, the grade steps to the LEFT corner when there is one
                // (the card's corner-start slot), and the plate takes what is left in between.
                var right = rect.Right - 4;
                if (tile.CornerLabel != null)
                    right = DrawCorner(canvas, tile.CornerLabel, tile.CornerHex ?? card.AccentHex, right,
                        rect.Bottom - 4, card.InkHex);

                if (plate != null)
                {
                    canvas.DrawBitmap(plate,
                        SKRect.Create(right - GradeWidth, rect.Bottom - GradeHeight - 4, GradeWidth, GradeHeight));
                    right -= GradeWidth + 4;
                }

                if (grade != null)
                    canvas.DrawBitmap(grade, tile.CornerLabel == null
                        ? SKRect.Create(right - GradeWidth, rect.Bottom - GradeHeight - 4, GradeWidth, GradeHeight)
                        : SKRect.Create(rect.Left + 4, rect.Bottom - GradeHeight - 4, GradeWidth, GradeHeight));

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
                        using (var border = new SKPaint
                               {
                                   Style = SKPaintStyle.Stroke, StrokeWidth = 2,
                                   Color = SKColor.Parse(tile.BadgeHex), IsAntialias = true,
                                   PathEffect = DashFor(tile.Outline)
                               })
                        {
                            canvas.DrawRoundRect(new SKRoundRect(SKRect.Inflate(rect, -1, -1), 6), border);
                        }
                }

                x += TileWidth + Pad;
            }

            y += TileHeight + Pad;
        }

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
