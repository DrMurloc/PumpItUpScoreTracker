using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The published tier lists behind one route. v1 spends an action per list for an identical
///     shape; here the list is a path value, so another list is data rather than a deployment.
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/tier-lists")]
public sealed class TierListsController : ApiV2ControllerBase
{
    /// <summary>
    ///     Route value to the stored list name, per scoring era.
    ///     <para>
    ///         Three lists, each named for the question it answers rather than for the column it is
    ///         computed from — "Score Difficulty" is what a reader wants; "Scores" is an
    ///         implementation detail that reads like a score list. The blend inputs
    ///         (<c>Chabala</c>), the mirror-derived <c>Official Scores</c> and <c>Popularity</c> are
    ///         deliberately not published: they are visible on /TierLists but they are not
    ///         difficulty judgements, and an integrator sorting by "popularity" would be sorting by
    ///         something else entirely (owner, 2026-08-02).
    ///     </para>
    ///     <para>
    ///         Pass difficulty reads from a different table row before Phoenix. The pre-Phoenix
    ///         lists were built as a single <c>Difficulty</c> list rather than split by question, so
    ///         the route stays stable and the mapping moves — a caller asking one mix for pass
    ///         difficulty and then another gets the same shape and the same meaning.
    ///     </para>
    /// </summary>
    private static readonly Dictionary<string, (string? Phoenix, string? Legacy)> Lists =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["score-difficulty"] = ("Scores", null),
            ["pass-difficulty"] = ("Pass Count", "Difficulty"),
            ["pg-difficulty"] = ("PG", null)
        };

    private readonly IMediator _mediator;

    public TierListsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <param name="listType">
    ///     <c>score-difficulty</c> · <c>pass-difficulty</c> · <c>pg-difficulty</c>. Only
    ///     <c>pass-difficulty</c> exists before Phoenix — the other two describe a scoring model
    ///     those mixes did not have.
    /// </param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <remarks>
    ///     Returns the raw list for the requested mix. Unlike the site, the API never substitutes
    ///     another mix's data for an empty list — expect an empty array rather than a response whose
    ///     meaning silently changes later.
    /// </remarks>
    [HttpGet("{listType}")]
    [ProducesResponseType(typeof(CursorPageDto<TierListEntryV2Dto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] string listType,
        [FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!Lists.TryGetValue(listType, out var names))
            return NotFoundProblem($"Unknown tier list. Valid values: {string.Join(", ", Lists.Keys)}");

        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        // 404 rather than an empty 200: "this list does not exist for this mix" and "this list is
        // empty for this mix" are different answers, and a caller that cannot tell them apart will
        // read a missing scoring model as missing data and wait for it to arrive.
        var name = mix.UsesLegacyScoring() ? names.Legacy : names.Phoenix;
        if (name is null)
            return NotFoundProblem($"{listType} isn't published for {mix}. " +
                                   $"Available for this mix: {string.Join(", ", AvailableFor(mix))}");

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

    private static IEnumerable<string> AvailableFor(MixEnum mix)
    {
        return Lists.Where(l => (mix.UsesLegacyScoring() ? l.Value.Legacy : l.Value.Phoenix) is not null)
            .Select(l => l.Key);
    }
}
