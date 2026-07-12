using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Configuration;

namespace ScoreTracker.Web.Pages;

/// <summary>
///     The logged-out front door (docs/design/front-door.md) — a real Razor Page, not a
///     Blazor component, so crawlers and link-unfurlers get complete HTML with no
///     SignalR circuit (the app-wide prerender flip is off the table). Every query it
///     dispatches is served from an in-process cache (D7).
/// </summary>
public sealed class FrontDoorModel : PageModel
{
    private readonly IOptions<DevAuthConfiguration> _devAuth;
    private readonly IMediator _mediator;

    public FrontDoorModel(IMediator mediator, IOptions<DevAuthConfiguration> devAuth)
    {
        _mediator = mediator;
        _devAuth = devAuth;
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Same guards the Blazor login page had: signed-in users skip the door, and a
        // fresh local database routes the developer to the populate harness.
        if (User.Identity?.IsAuthenticated == true) return Redirect("/Charts");
        if (_devAuth.Value.Enabled &&
            !(await _mediator.Send(new GetChartsQuery(MixEnum.Phoenix), cancellationToken)).Any())
            return Redirect("/Dev/Populate");

        Ledger = await _mediator.Send(new GetLedgerActivityStatsQuery(), cancellationToken);
        Playerbase = await _mediator.Send(new GetPlayerbaseStatsQuery(), cancellationToken);
        CommunityCount = await _mediator.Send(new GetCommunityCountQuery(), cancellationToken);

        var counts = Ledger.DailyVolumes.Select(v => v.Count).OrderBy(c => c).ToArray();
        PulseCap = Math.Max(counts[(int)Math.Floor((counts.Length - 1) * 0.9)], 1);
        return Page();
    }
}
