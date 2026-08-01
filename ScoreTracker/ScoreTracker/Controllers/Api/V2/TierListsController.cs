using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The four tier lists behind one route. v1 spends an action per list for an identical shape;
///     here the list is a path value, so a fifth list is data rather than a deployment.
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/tier-lists")]
public sealed class TierListsController : ApiV2ControllerBase
{
    /// <summary>Route value to the stored tier list name.</summary>
    private static readonly Dictionary<string, string> Lists = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scores"] = "Scores",
        ["official-scores"] = "Official Scores",
        ["pass-count"] = "Pass Count",
        ["popularity"] = "Popularity"
    };

    private readonly IMediator _mediator;

    public TierListsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <param name="listType">scores · official-scores · pass-count · popularity</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <remarks>
    ///     Returns the raw list for the requested mix. Unlike the site, the API never substitutes
    ///     another mix's data for an empty list — expect an empty array rather than a response whose
    ///     meaning silently changes later.
    /// </remarks>
    [HttpGet("{listType}")]
    public async Task<IActionResult> Get([FromRoute] string listType,
        [FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!Lists.TryGetValue(listType, out var name))
            return NotFoundProblem($"Unknown tier list. Valid values: {string.Join(", ", Lists.Keys)}");

        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        // The fallback-aware query, with provisional results discarded, is exactly the raw per-mix
        // list. GetTierListQuery bakes in the site's silent Phoenix-1 stand-in for an empty Phoenix 2
        // list, which would flip an integrator's responses the day Phoenix 2 votes accumulate.
        var result = await _mediator.Send(new GetTierListWithFallbackQuery(Name.From(name), mix));
        var rows = (result.IsProvisionalFallback ? Array.Empty<TierListEntryV2Dto>() : result.Entries
                .OrderBy(e => e.Category).ThenBy(e => e.Order)
                .Select(e => new TierListEntryV2Dto
                {
                    ChartId = e.ChartId,
                    Category = e.Category.ToString(),
                    Order = e.Order
                }).ToArray());

        return CatalogJson(Page(rows, rows.Length, rows.Length, null));
    }
}
