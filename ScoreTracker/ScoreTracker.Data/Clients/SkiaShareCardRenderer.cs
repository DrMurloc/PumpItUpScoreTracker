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
            .SelectMany(t => new[] { t.JacketUrl, t.GradeUrl, t.PlateUrl })
            .Append(card.BubbleUrl)
            .Where(u => u != null)
            .Select(u => u!)
            .Distinct();
        await Task.WhenAll(allUrls.Select(u => LoadImage(u, cancellationToken)));

        var bubble = card.BubbleUrl == null ? null : await LoadImage(card.BubbleUrl, cancellationToken);
        var tiles = new Dictionary<TierListShareCard.Tile, (SKBitmap? Jacket, SKBitmap? Grade, SKBitmap? Plate)>();
        foreach (var tile in card.Rows.SelectMany(r => r.Tiles))
            tiles[tile] = (await LoadImage(tile.JacketUrl, cancellationToken),
                tile.GradeUrl == null ? null : await LoadImage(tile.GradeUrl, cancellationToken),
                tile.PlateUrl == null ? null : await LoadImage(tile.PlateUrl, cancellationToken));

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
                var (jacket, grade, plate) = tiles[tile];
                if (jacket != null)
                {
                    canvas.Save();
                    canvas.ClipRoundRect(new SKRoundRect(rect, 6));
                    canvas.DrawBitmap(jacket, rect);
                    canvas.Restore();
                }

                if (plate != null)
                    canvas.DrawBitmap(plate,
                        SKRect.Create(rect.Right - GradeWidth - 4, rect.Bottom - GradeHeight - 4, GradeWidth,
                            GradeHeight));
                if (grade != null)
                    canvas.DrawBitmap(grade,
                        SKRect.Create(rect.Right - (GradeWidth + 4) * 2, rect.Bottom - GradeHeight - 4, GradeWidth,
                            GradeHeight));
                if (tile.BadgeHex != null)
                    using (var badge = new SKPaint
                           {
                               Style = SKPaintStyle.Fill, Color = SKColor.Parse(tile.BadgeHex), IsAntialias = true
                           })
                    {
                        canvas.DrawCircle(rect.Right - 9, rect.Top + 9, 6, badge);
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
