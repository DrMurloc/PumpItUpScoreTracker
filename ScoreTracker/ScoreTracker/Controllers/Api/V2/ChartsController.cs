using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Application.Queries;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     The chart catalog for one mix. Level, note count and legacy slot are per-mix facts, which is
///     why <c>mix</c> is required rather than optional — the same chart id is S17 in one mix and S18
///     in another.
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/charts")]
public sealed class ChartsController : ApiV2ControllerBase
{
    private readonly IMediator _mediator;

    public ChartsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="level">Optional difficulty level filter.</param>
    /// <param name="typeValue">Optional chart type filter: Single, Double, CoOp, SinglePerformance, DoublePerformance.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<ChartV2Dto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "level")] int? level = null,
        [FromQuery(Name = "type")] string? typeValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var failure)) return failure!;

        ChartType? type = null;
        if (typeValue is not null)
        {
            if (!Enum.TryParse<ChartType>(typeValue, true, out var parsed))
                return Problem("invalid-chart-type", "The type parameter is not a chart type.",
                    detail: $"Valid values: {string.Join(", ", Enum.GetNames<ChartType>())}");
            type = parsed;
        }

        if (level is not null && !DifficultyLevel.IsValid(level.Value))
            return Problem("invalid-level", "The level parameter is out of range.",
                detail: $"Valid range: {DifficultyLevel.Min}–{DifficultyLevel.Max}");

        var fingerprint = ContinuationToken.FingerprintOf(mix, level, type, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var charts = (await _mediator.Send(new GetChartsQuery(mix,
                level is null ? null : DifficultyLevel.From(level.Value), type)))
            .OrderBy(c => c.Id)
            .ToArray();

        // Scoring difficulty is chart metadata, not a separate resource — it keys on (chart, mix),
        // exactly the grain this DTO already has. One dictionary read per page rather than a second
        // endpoint an integrator has to know to join.
        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix));
        var rows = charts.Skip(offset).Take(pageSize)
            .Select(c => new ChartV2Dto(c, scoringLevels.TryGetValue(c.Id, out var sl) ? sl : null))
            .ToArray();
        var next = offset + rows.Length < charts.Length
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return CatalogJson(Page(rows, pageSize, charts.Length, next));
    }


    /// <summary>
    ///     PIU Center's step analysis for one chart: NPS, difficulty prediction, sustain, and
    ///     per-skill coverage.
    /// </summary>
    /// <remarks>Mix-invariant — it describes the steps, so no <c>mix</c> parameter.</remarks>
    [HttpGet("{chartId:guid}/skills")]
    [ProducesResponseType(typeof(ChartSkillProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChartSkills([FromRoute] Guid chartId)
    {
        var profiles = await _mediator.Send(new GetChartSkillProfilesQuery(new[] { chartId }));
        var profile = profiles.FirstOrDefault();

        // Distinguished from "no such chart" on purpose: most of the catalog is unanalysed, and a
        // reader who cannot tell the two apart will chase a chart id that is perfectly valid.
        if (profile is null)
            return NotFoundProblem("That chart has no step analysis. Most of the catalog does not — " +
                                   "PIU Center analyses a subset, and a chart being absent here says " +
                                   "nothing about whether it exists.");

        return CatalogJson(new ChartSkillProfileDto(profile));
    }

    /// <summary>Step analysis in bulk, taking the same filters as <c>GET /api/v2/charts</c>.</summary>
    /// <remarks>
    ///     <c>mix</c> is required because the filters are per-mix. The analysis itself is
    ///     mix-invariant, so do not cache it per mix.
    /// </remarks>
    // Written out rather than a see cref: Swashbuckle renders a cref as its display name, and for a
    // method that is the whole signature — the reader would get System.Nullable{System.Int32} in
    // the endpoint description. XML doc reaches Swagger; a line comment does not, which is why the
    // rationale lives down here.
    [HttpGet("skills")]
    [ProducesResponseType(typeof(CursorPageDto<ChartSkillProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSkills(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "level")] int? level = null,
        [FromQuery(Name = "type")] string? typeValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var failure)) return failure!;

        ChartType? type = null;
        if (typeValue is not null)
        {
            if (!Enum.TryParse<ChartType>(typeValue, true, out var parsed))
                return Problem("invalid-chart-type", "The type parameter is not a chart type.",
                    detail: $"Valid values: {string.Join(", ", Enum.GetNames<ChartType>())}");
            type = parsed;
        }

        if (level is not null && !DifficultyLevel.IsValid(level.Value))
            return Problem("invalid-level", "The level parameter is out of range.",
                detail: $"Valid range: {DifficultyLevel.Min}–{DifficultyLevel.Max}");

        var fingerprint = ContinuationToken.FingerprintOf(mix, level, type, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var chartIds = (await _mediator.Send(new GetChartsQuery(mix,
                level is null ? null : DifficultyLevel.From(level.Value), type)))
            .Select(c => c.Id).ToArray();

        var profiles = (await _mediator.Send(new GetChartSkillProfilesQuery(chartIds)))
            .OrderBy(p => p.ChartId).ToArray();

        var rows = profiles.Skip(offset).Take(pageSize).Select(p => new ChartSkillProfileDto(p)).ToArray();
        var next = offset + rows.Length < profiles.Length
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return CatalogJson(Page(rows, pageSize, profiles.Length, next));
    }

    /// <summary>One chart, as expressed in the requested mix.</summary>
    [HttpGet("{chartId:guid}")]
    [ProducesResponseType(typeof(ChartV2Dto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOne([FromRoute] Guid chartId,
        [FromQuery(Name = "mix")] string? mixValue = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var chart = (await _mediator.Send(new GetChartsQuery(mix)))
            .FirstOrDefault(c => c.Id == chartId);
        if (chart is null) return NotFoundProblem("No chart with that id exists in this mix.");

        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix));
        return CatalogJson(new ChartV2Dto(chart,
            scoringLevels.TryGetValue(chart.Id, out var scoringLevel) ? scoringLevel : null));
    }

    /// <summary>Charts that play like this one, best first.</summary>
    /// <remarks>
    ///     Rows below <c>matchFloor</c> are near-misses rather than absences — nothing is filtered
    ///     out by quality, so where the bar falls is yours to decide.
    /// </remarks>
    // Filters narrow what the anchor is compared against and the scores are recomputed; they never
    // sieve a precalculated list, which would return nothing for any filter narrow enough to be
    // interesting. That also makes this the out-of-window path — what D23s play like a D18 is a real
    // question and deliberately outside the ±1 the nightly job precalculates. See
    // docs/design/chart-similarity.md; a line comment keeps it out of the Swagger description.
    [HttpGet("{chartId:guid}/similar")]
    [ProducesResponseType(typeof(SimilarChartsDto), StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSimilar([FromRoute] Guid chartId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "minLevel")] int? minLevel = null,
        [FromQuery(Name = "maxLevel")] int? maxLevel = null,
        [FromQuery(Name = "minScoringLevel")] double? minScoringLevel = null,
        [FromQuery(Name = "maxScoringLevel")] double? maxScoringLevel = null,
        [FromQuery(Name = "minBpm")] decimal? minBpm = null,
        [FromQuery(Name = "maxBpm")] decimal? maxBpm = null,
        [FromQuery(Name = "minNps")] double? minNps = null,
        [FromQuery(Name = "maxNps")] double? maxNps = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();

        var result = await _mediator.Send(new GetFilteredSimilarChartsQuery(chartId, mix, minLevel, maxLevel,
            minScoringLevel, maxScoringLevel, minBpm, maxBpm, minNps, maxNps));

        return Json(new SimilarChartsDto
        {
            ChartsCompared = result.ChartsCompared,
            MatchFloor = ChartSimilarityRecord.MatchFloor,
            Data = result.Matches.Select(m => new SimilarChartDto
            {
                ChartId = m.ChartId,
                Score = m.Score,
                SkillScore = m.SkillScore,
                IntensityScore = m.IntensityScore,
                SharedBadges = m.SharedBadges
                    .Select(b => new SharedBadgeDto { Badge = b.Badge, Coverage = b.Coverage }).ToArray()
            }).ToArray()
        });
    }

    /// <summary>
    ///     A weighted random draw — the engine behind the site's randomizer.
    /// </summary>
    /// <param name="buckets">
    ///     Minimum pull counts. "Single:2" pulls at least 2 singles; "S19:8" at least 8 S19s;
    ///     "S21,S22,D23:4" at least 4 from that set of folders. Bucket minimums win over
    ///     <paramref name="count" /> when they exceed it.
    /// </param>
    [HttpGet("random")]
    [ProducesResponseType(typeof(CursorPageDto<ChartV2Dto>), StatusCodes.Status200OK)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRandom(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "count")] int count = 5,
        [FromQuery(Name = "chartTypes")] string[]? chartTypes = null,
        [FromQuery(Name = "songTypes")] string[]? songTypes = null,
        [FromQuery(Name = "minLevel")] int? minLevel = null,
        [FromQuery(Name = "maxLevel")] int? maxLevel = null,
        [FromQuery(Name = "bucket")] string[]? buckets = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();
        if (count < 1) return Problem("invalid-count", "count must be at least 1.");

        var settings = new RandomSettings { Count = count };

        var types = chartTypes is null
            ? new[] { ChartType.Single, ChartType.Double }
            : chartTypes.Where(s => Enum.TryParse<ChartType>(s, true, out _))
                .Select(s => Enum.Parse<ChartType>(s, true)).ToArray();

        if (songTypes is not null)
        {
            if (songTypes.Any(s => !Enum.TryParse<SongType>(s, true, out _)))
                return Problem("invalid-song-type", "songTypes contains a value that is not a song type.",
                    detail: $"Valid values: {string.Join(", ", Enum.GetNames<SongType>())}");
            foreach (var type in songTypes.Select(s => Enum.Parse<SongType>(s, true)))
                settings.SongTypeWeights[type] = 1;
        }
        else
        {
            foreach (var type in Enum.GetValues<SongType>()) settings.SongTypeWeights[type] = 1;
        }

        foreach (var bucket in buckets ?? Array.Empty<string>())
        {
            var split = bucket.Split(":");
            if (split.Length != 2 || !int.TryParse(split[1], out var weight) || weight < 1)
                return BucketProblem(bucket);

            if (Enum.TryParse<ChartType>(split[0], true, out var bucketType))
            {
                settings.ChartTypeMinimums[bucketType] = weight;
            }
            else if (DifficultyLevel.TryParse(split[0], out var bucketLevel))
            {
                settings.LevelMinimums[bucketLevel] = weight;
            }
            else
            {
                var folders = split[0].Split(",");
                if (folders.Any(f => !DifficultyLevel.TryParseShortHand(f, out _, out _)))
                    return BucketProblem(bucket);

                if (folders.Length == 1) settings.ChartTypeLevelMinimums[folders[0]] = weight;
                else settings.CustomMinimums[split[0]] = weight;
            }
        }

        if (minLevel is not null && !DifficultyLevel.IsValid(minLevel.Value)) return LevelRangeProblem();
        if (maxLevel is not null && !DifficultyLevel.IsValid(maxLevel.Value)) return LevelRangeProblem();
        if (minLevel > maxLevel)
            return Problem("invalid-level", "minLevel must not exceed maxLevel.");

        for (var level = minLevel ?? DifficultyLevel.Min; level <= (maxLevel ?? DifficultyLevel.Max); level++)
        {
            if (types.Contains(ChartType.Single)) settings.LevelWeights[level] = 1;
            if (types.Contains(ChartType.Double)) settings.DoubleLevelWeights[level] = 1;
            if (types.Contains(ChartType.CoOp) && level <= 5) settings.PlayerCountWeights[level] = 1;
        }

        try
        {
            var charts = await _mediator.Send(new GetRandomChartsQuery(settings, mix));
            var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(mix));
            var rows = charts
                .Select(c => new ChartV2Dto(c, scoringLevels.TryGetValue(c.Id, out var sl) ? sl : null))
                .ToArray();
            return Json(Page(rows, rows.Length, rows.Length, null));
        }
        catch (RandomizerException e)
        {
            // A domain exception's message is written to be read by a player, which is the one
            // category DiagnosticExposureTests allows through.
            return Problem("randomizer-cannot-satisfy", e.Message);
        }
    }

    private ObjectResult BucketProblem(string bucket)
    {
        return Problem("invalid-bucket", $"'{bucket}' is not a valid bucket.",
            detail: "Examples: 'Single:5', '22:3', 'D23:2', 'S21,S22,D23:4'");
    }

    private ObjectResult LevelRangeProblem()
    {
        return Problem("invalid-level", "A level parameter is out of range.",
            detail: $"Valid range: {DifficultyLevel.Min}–{DifficultyLevel.Max}");
    }
}
