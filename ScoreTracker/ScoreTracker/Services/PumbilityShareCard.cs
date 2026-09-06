using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.JSInterop;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.Theming;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Web.Services;

/// <summary>
///     One tile of a downloadable PUMBILITY card, as facts rather than presentation: what the
///     row knows, read off the row itself, so the picture cannot drift from the page. How the
///     facts are drawn is the share-card composer's call under the download settings
///     (docs/design/share-card-download-settings.md).
/// </summary>
/// <param name="Score">
///     Your best, including a broken one — the composer's broken gating needs the number, where
///     a list's own row may deliberately hide a broken best behind null.
/// </param>
/// <param name="Gain">What the chart would add, only when it pays — the list's own rule.</param>
/// <param name="Expected">The projected score whose grade rides the gain chip.</param>
/// <param name="PoolValue">
///     What the chart is worth in your pool, where the frame already computed it — the exact
///     number the Breakdown page prints, so the chip and the page cannot disagree. Null on a row
///     the frame did not price, and the composer prices it through the same formula then.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record PumbilityShareTile(Chart Chart, PhoenixScore? Score, PhoenixPlate? Plate, bool Broken,
    bool IsToDo, bool Carried, double? Gain, PhoenixScore? Expected, double? PoolValue);

/// <summary>
///     One section of a downloadable PUMBILITY card: its printed name, the ramp category it took
///     its colour from (null for a gain band, which is not on the ramp), and its tiles in order.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PumbilityShareSection(string Name, TierListCategory? Tier, IReadOnlyList<PumbilityShareTile> Tiles);

/// <summary>
///     The share-card build and download the PUMBILITY pages share
///     (docs/design/pumbility-overhaul.md §6.11). The Play list and the Breakdown page's top 50
///     are one card model — the tier list's own — so this owns the tile facts, the row
///     composition, the example the dialog shows, the top-50 memberships the gold boundary asks
///     for, and the fetch-render-stream tail; a page keeps only what differs, its header and its
///     file name. One instance per page circuit: the memberships it caches are the viewer's.
/// </summary>
public sealed class PumbilityShareCard
{
    private readonly IMediator _mediator;
    private readonly IJSRuntime _js;
    private readonly Func<string, string> _localize;
    private readonly Guid _userId;

    // The top-50 membership reads, fetched when the gold boundary first asks and dropped when the
    // page re-reads — the answer is per mix and per type, and a membership must never outlive
    // the list it was read for.
    private readonly Dictionary<ChartType, IReadOnlySet<Guid>> _top50ByType = new();
    private IReadOnlySet<Guid>? _top50Combined;

    public PumbilityShareCard(IMediator mediator, IJSRuntime js, Guid userId, Func<string, string> localize)
    {
        _mediator = mediator;
        _js = js;
        _userId = userId;
        _localize = localize;
    }

    /// <summary>Drops the cached memberships; the next download re-reads them for the list it is taken from.</summary>
    public void Forget()
    {
        _top50ByType.Clear();
        _top50Combined = null;
    }

    /// <summary>
    ///     The card's rows off a rendered list's sections — the same sections in the same order,
    ///     each in its own ramp colour, so the image says what the screen says. Reads the top-50
    ///     memberships first when the options ask for that boundary.
    /// </summary>
    public async Task<IReadOnlyList<TierListShareCard.Row>> ComposeRows(IReadOnlyList<PumbilityShareSection> sections,
        ShareCardOptions options, MixEnum mix,
        IReadOnlyDictionary<Guid, IReadOnlyList<TierListChartCard.CardSkillChip>> skills,
        CancellationToken cancellationToken)
    {
        await EnsureMemberships(options, sections, mix, cancellationToken);
        var palette = MixThemes.PaletteFor(mix);
        return sections
            .Where(s => s.Tiles.Count > 0)
            .Select(s => new TierListShareCard.Row(s.Name,
                s.Tier is { } tier ? MixThemes.PumbilityHex(mix, tier) : palette.Primary,
                s.Tiles.Select(t => ShareCardComposer.Compose(Facts(t, options, mix, skills), options, mix, palette))
                    .ToArray()))
            .ToArray();
    }

    /// <summary>The card itself: the page's header over the rows, in the mix's palette, with the legend the options earn.</summary>
    public TierListShareCard Card(ShareCardTitles.Header header, string url, IReadOnlyList<TierListShareCard.Row> rows,
        ShareCardOptions options, MixEnum mix)
    {
        var palette = MixThemes.PaletteFor(mix);
        return new TierListShareCard(
            header.Title,
            header.Subtitle,
            header.Stamp ?? string.Empty,
            palette.Primary, palette.Background, palette.Surface, palette.Ink, palette.InkMuted,
            url,
            null,
            rows,
            ShareCardComposer.Legend(options, mix, palette, _localize));
    }

    /// <summary>
    ///     The dialog's example: the list's first six jackets wearing the scripted states
    ///     (share-card design doc §10) — stage props, so every option shows at once and no top-50
    ///     read runs for a preview. Null when the list has nothing to borrow a jacket from.
    /// </summary>
    public TierListShareCard? Sample(IReadOnlyList<Chart> charts, ShareCardOptions options, MixEnum mix,
        IReadOnlyDictionary<Guid, IReadOnlyList<TierListChartCard.CardSkillChip>> skills,
        ShareCardTitles.Header header, string url)
    {
        var picked = charts.Take(ShareCardSample.Size).ToArray();
        if (picked.Length == 0) return null;
        var palette = MixThemes.PaletteFor(mix);
        var facts = ShareCardSample.Facts(picked, mix,
            c => options.Skills && skills.TryGetValue(c.Id, out var chips) ? chips : null,
            ShareCardImages.DifficultyBubble);
        var rows = new[]
        {
            new TierListShareCard.Row(_localize("Example"), palette.Primary,
                facts.Select(f => ShareCardComposer.Compose(f, options, mix, palette)).ToArray())
        };
        return Card(header, url, rows, options, mix);
    }

    /// <summary>
    ///     The slow phase, driven from here so the dialog's bar counts real images (share-card
    ///     design doc §8): warm the card's art in batches, render against the warm cache, and
    ///     hand the PNG to the browser. The token is the dialog's Cancel — and any close of it —
    ///     and the caller swallows the cancellation it raises.
    /// </summary>
    public async Task Download(TierListShareCard card, ShareCardDownloadRequest request, string fileName)
    {
        var urls = ShareCardArt.CollectUrls(card);
        var done = 0;
        request.Progress(new ShareCardFetchProgress(0, urls.Count, false));
        foreach (var batch in urls.Chunk(ShareCardArt.FetchBatch))
        {
            await _mediator.Send(new PrefetchShareCardArtCommand(batch), request.Token);
            done += batch.Length;
            request.Progress(new ShareCardFetchProgress(done, urls.Count, false));
        }

        request.Progress(new ShareCardFetchProgress(urls.Count, urls.Count, true));
        var bytes = await _mediator.Send(new GetTierListShareCardQuery(card), request.Token);
        using var stream = new MemoryStream(bytes);
        using var streamRef = new DotNetStreamReference(stream);
        var module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/helpers.js");
        await module.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
    }

    private async Task EnsureMemberships(ShareCardOptions options, IReadOnlyList<PumbilityShareSection> sections,
        MixEnum mix, CancellationToken cancellationToken)
    {
        if (!options.BoundaryTop50) return;
        _top50Combined ??= (await _mediator.Send(new GetTop50ForPlayerQuery(_userId, null, 50, mix), cancellationToken))
            .Select(s => s.ChartId).ToHashSet();
        foreach (var type in sections.SelectMany(s => s.Tiles).Select(t => t.Chart.Type).Distinct())
            if (!_top50ByType.ContainsKey(type))
                _top50ByType[type] = (await _mediator.Send(new GetTop50ForPlayerQuery(_userId, type, 50, mix), cancellationToken))
                    .Select(s => s.ChartId).ToHashSet();
    }

    private ShareCardComposer.TileFacts Facts(PumbilityShareTile tile, ShareCardOptions options, MixEnum mix,
        IReadOnlyDictionary<Guid, IReadOnlyList<TierListChartCard.CardSkillChip>> skills)
    {
        // The pool's own value wins where the frame already computed it; anything else prices
        // through the same formula the Breakdown page uses, so the chip never disagrees with it.
        var current = options.Pumbility && !options.ExpectedGains
            ? tile.PoolValue ?? ShareCardComposer.CurrentPumbility(tile.Chart, tile.Score, tile.Plate, tile.Broken, mix)
            : null;
        return new ShareCardComposer.TileFacts(tile.Chart, tile.Score, tile.Plate,
            Passed: tile.Score != null && !tile.Broken,
            Broken: tile.Broken,
            IsToDo: tile.IsToDo,
            PassedInOtherMix: tile.Carried,
            InTop50Type: _top50ByType.TryGetValue(tile.Chart.Type, out var typed) && typed.Contains(tile.Chart.Id),
            InTop50Combined: _top50Combined?.Contains(tile.Chart.Id) ?? false,
            tile.Gain, tile.Expected, current,
            options.Skills && skills.TryGetValue(tile.Chart.Id, out var chips) ? chips : null,
            // Every difficulty at once: without the bubble on the tile the picture cannot say
            // what any of it is (owner, field test round six; design doc §5).
            ShareCardImages.DifficultyBubble(tile.Chart));
    }
}
