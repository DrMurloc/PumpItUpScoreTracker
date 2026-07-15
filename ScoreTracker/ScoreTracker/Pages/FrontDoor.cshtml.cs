using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Configuration;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

namespace ScoreTracker.Web.Pages;

/// <summary>
///     The logged-out front door (docs/design/front-door.md) — a real Razor Page, not a
///     Blazor component, so crawlers and link-unfurlers get complete HTML with no
///     SignalR circuit (the app-wide prerender flip is off the table). Every query it
///     dispatches is either cache-backed at its repository or cached here (D7).
/// </summary>
public sealed class FrontDoorModel : PageModel
{
    /// <summary>
    ///     The showcase folder: Singles 21 — the heart of the casual-competitive 17–23
    ///     range the site serves most.
    /// </summary>
    public const int ShowcaseLevel = 21;

    private const string ShowcaseCacheKey = "FrontDoor_Showcase";

    private readonly IMemoryCache _cache;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IOptions<DevAuthConfiguration> _devAuth;
    private readonly IMediator _mediator;

    public FrontDoorModel(IMediator mediator, IOptions<DevAuthConfiguration> devAuth, IMemoryCache cache,
        IDateTimeOffsetAccessor clock)
    {
        _mediator = mediator;
        _devAuth = devAuth;
        _cache = cache;
        _clock = clock;
    }

    public LedgerActivityStats Ledger { get; private set; } = null!;
    public PlayerbaseCounts Playerbase { get; private set; } = null!;
    public int CommunityCount { get; private set; }

    /// <summary>
    ///     Pulse bars clip here (≈p90 of the window) so a feature-drop spike renders as
    ///     a maxed bar instead of flattening the other 29 days; the printed total stays
    ///     exact (front-door.md D6).
    /// </summary>
    public int PulseCap { get; private set; }

    public IReadOnlyList<FrontDoorTierBand> TierBands { get; private set; } = Array.Empty<FrontDoorTierBand>();
    public IReadOnlyList<FrontDoorWeeklyRow> WeeklyRows { get; private set; } = Array.Empty<FrontDoorWeeklyRow>();
    public int WeeklyMoreCount { get; private set; }
    public DateTimeOffset? WeeklyExpiration { get; private set; }

    /// <summary>Time until the weekly board rotates — never negative, null when no board.</summary>
    public TimeSpan? WeeklyRotatesIn => WeeklyExpiration is { } exp
        ? exp > _clock.Now ? exp - _clock.Now : TimeSpan.Zero
        : null;

    /// <summary>"1M+"-style short form for the tier-list card's provenance tag.</summary>
    public string ScoreBasisShort => Ledger.TotalRecords >= 1_000_000
        ? $"{Ledger.TotalRecords / 1_000_000d:0.#}M+"
        : Ledger.TotalRecords.ToString("N0");

    /// <summary>
    ///     The front door is for visitors who aren't signed in — it owns "/Welcome" and
    ///     "/Login", and the dashboard owns "/". A signed-in visitor who lands here has
    ///     nothing to see, so they bounce home.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");

        // A fresh local database routes the developer to the populate harness.
        if (_devAuth.Value.Enabled &&
            !(await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).Any())
            return Redirect("/Dev/Populate");

        Ledger = await _mediator.Send(new GetLedgerActivityStatsQuery(), cancellationToken);
        Playerbase = await _mediator.Send(new GetPlayerbaseStatsQuery(), cancellationToken);
        CommunityCount = await _mediator.Send(new GetCommunityCountQuery(), cancellationToken);

        var counts = Ledger.DailyVolumes.Select(v => v.Count).OrderBy(c => c).ToArray();
        PulseCap = Math.Max(counts[(int)Math.Floor((counts.Length - 1) * 0.9)], 1);

        var showcase = (await _cache.GetOrCreateAsync(ShowcaseCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await ComposeShowcase(cancellationToken);
        }))!;
        TierBands = showcase.TierBands;
        WeeklyRows = showcase.WeeklyRows;
        WeeklyMoreCount = showcase.WeeklyMoreCount;
        // The countdown text renders from this timestamp per request, so it stays
        // honest while the board composition itself is cached.
        WeeklyExpiration = showcase.WeeklyExpiration;

        return Page();
    }

    private async Task<ShowcaseData> ComposeShowcase(CancellationToken cancellationToken)
    {
        var charts = (await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken))
            .ToDictionary(c => c.Id);

        // The community Pass tier list, filtered to the showcase folder. Bands render
        // in difficulty order with up to two named charts each — the live page shows
        // whatever the data says, never a curated placement.
        var passEntries = await _mediator.Send(new GetTierListQuery("Pass"), cancellationToken);
        var folder = passEntries
            .Where(e => charts.TryGetValue(e.ChartId, out var c) &&
                        c.Type == ChartType.Single && (int)c.Level == ShowcaseLevel)
            .ToArray();
        var bands = new[]
            {
                TierListCategory.Easy, TierListCategory.Medium,
                TierListCategory.Hard, TierListCategory.VeryHard
            }
            .Select(category =>
            {
                var inBand = folder.Where(e => e.Category == category)
                    .OrderBy(e => e.Order)
                    .Select(e => charts[e.ChartId])
                    .ToArray();
                return new FrontDoorTierBand(category, inBand.Take(2).ToArray(),
                    Math.Max(inBand.Length - 2, 0));
            })
            .Where(b => b.Charts.Count > 0)
            .ToArray();

        var weeklyCharts = (await _mediator.Send(new GetWeeklyChartsQuery(), cancellationToken))
            .Where(w => charts.ContainsKey(w.ChartId))
            .ToArray();
        var leaders = (await _mediator.Send(new GetWeeklyChartEntriesQuery(), cancellationToken))
            .Where(e => !e.IsBroken)
            .GroupBy(e => e.ChartId)
            .ToDictionary(g => g.Key, g => g.Max(e => (int)e.Score));
        var rows = weeklyCharts
            .OrderByDescending(w => leaders.ContainsKey(w.ChartId))
            .ThenBy(w => (int)charts[w.ChartId].Level)
            .Take(2)
            .Select(w => new FrontDoorWeeklyRow(charts[w.ChartId],
                leaders.TryGetValue(w.ChartId, out var s) ? s : null))
            .ToArray();

        return new ShowcaseData(bands, rows, Math.Max(weeklyCharts.Length - rows.Length, 0),
            weeklyCharts.Length > 0 ? weeklyCharts.Min(w => w.ExpirationDate) : null);
    }

    private sealed record ShowcaseData(
        IReadOnlyList<FrontDoorTierBand> TierBands,
        IReadOnlyList<FrontDoorWeeklyRow> WeeklyRows,
        int WeeklyMoreCount,
        DateTimeOffset? WeeklyExpiration);
}

public sealed record FrontDoorTierBand(TierListCategory Category, IReadOnlyList<Chart> Charts, int MoreCount);

public sealed record FrontDoorWeeklyRow(Chart Chart, int? LeaderScore);
