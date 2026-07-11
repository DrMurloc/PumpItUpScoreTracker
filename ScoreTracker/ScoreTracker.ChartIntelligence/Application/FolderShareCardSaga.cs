using MassTransit;
using MediatR;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Regenerates the community share card for every Singles/Doubles folder and uploads
///     it to blob storage (tier-lists overhaul C9). The uploaded PNGs back the page's
///     per-folder og:image tags — a Discord unfurl of a folder link IS the tier list.
///     Community cards only: no personal grades, plates, or badges.
/// </summary>
internal sealed class FolderShareCardSaga : IConsumer<RefreshFolderShareCardsCommand>
{
    // Invariant English on purpose: one shared image per folder for every viewer.
    private static readonly IReadOnlyDictionary<TierListCategory, string> TierNames =
        new Dictionary<TierListCategory, string>
        {
            [TierListCategory.Underrated] = "1+ Level Harder",
            [TierListCategory.VeryHard] = "Very Hard",
            [TierListCategory.Hard] = "Hard",
            [TierListCategory.Medium] = "Medium",
            [TierListCategory.Easy] = "Easy",
            [TierListCategory.VeryEasy] = "Very Easy",
            [TierListCategory.Overrated] = "1+ Level Easier",
            [TierListCategory.Unrecorded] = "Not Rated"
        };

    private static readonly TimeSpan DelayPerFolder = TimeSpan.FromMilliseconds(250);

    private readonly IChartRepository _charts;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IFileUploadClient _files;
    private readonly IMediator _mediator;
    private readonly IShareCardRenderer _renderer;

    public FolderShareCardSaga(IChartRepository charts, IMediator mediator, IShareCardRenderer renderer,
        IFileUploadClient files, IDateTimeOffsetAccessor dateTime)
    {
        _charts = charts;
        _mediator = mediator;
        _renderer = renderer;
        _files = files;
        _dateTime = dateTime;
    }

    public static string SharePath(MixEnum mix, ChartType chartType, DifficultyLevel level)
    {
        return $"tier-lists/{mix}/{chartType.GetShortHand().ToLowerInvariant()}{level}.png";
    }

    public async Task Consume(ConsumeContext<RefreshFolderShareCardsCommand> context)
    {
        var mix = context.Message.Mix;
        var theme = context.Message.Theme;
        var cancellationToken = context.CancellationToken;
        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken)).ToArray();
        var folders = charts
            .Where(c => c.Type is ChartType.Single or ChartType.Double)
            .Select(c => (c.Type, c.Level))
            .Distinct()
            .OrderBy(f => f.Type).ThenBy(f => (int)f.Level);

        foreach (var (type, level) in folders)
        {
            var blend = await _mediator.Send(
                new GetBlendedTierListQuery(type, level, "Pass", Mix: mix), cancellationToken);
            var folderCharts = charts
                .Where(c => c.Type == type && c.Level == level)
                .ToDictionary(c => c.Id);
            var rows = blend.Entries
                .Where(e => folderCharts.ContainsKey(e.ChartId))
                .GroupBy(e => e.Category)
                .OrderByDescending(g => g.Key == TierListCategory.Unrecorded ? int.MinValue : (int)g.Key)
                .Select(g => new TierListShareCard.Row(TierNames[g.Key], theme.DifficultyHexes[g.Key],
                    g.OrderBy(e => e.Order)
                        .Select(e => new TierListShareCard.Tile(
                            folderCharts[e.ChartId].Song.ImagePath.ToString(), null, null, null))
                        .ToArray()))
                .Where(r => r.Tiles.Any())
                .ToArray();
            if (!rows.Any()) continue;

            var card = new TierListShareCard(
                $"{(type == ChartType.Single ? "Singles" : "Doubles")} {level}",
                $"Pass Difficulty · {mix} · {_dateTime.Now:yyyy-MM-dd}",
                "Community",
                theme.AccentHex,
                theme.BackgroundHex,
                theme.SurfaceHex,
                theme.InkHex,
                theme.InkMutedHex,
                $"https://piuscores.arroweclip.se/TierLists/{type}/{level}",
                $"https://piuimages.arroweclip.se/difficulty/{mix}/{type.GetShortHand().ToLowerInvariant()}{level}.png",
                rows);

            var bytes = await _renderer.RenderTierListCard(card, cancellationToken);
            await using var stream = new MemoryStream(bytes);
            await _files.UploadFile(SharePath(mix, type, level), stream, cancellationToken);

            // ~60 folders daily; stay gentle on the renderer's image fetches and blob writes.
            await Task.Delay(DelayPerFolder, cancellationToken);
        }
    }
}
