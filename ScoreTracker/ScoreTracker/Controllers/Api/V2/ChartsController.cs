using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Application.Queries;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
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

    /// <summary>The chart catalog for one mix, with each chart's level and note count as that mix lists them.</summary>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="level">Optional difficulty level filter.</param>
    /// <param name="typeValue">Optional chart type filter: Single, Double, CoOp, SinglePerformance, DoublePerformance.</param>
    /// <param name="addedIn">
    ///     Only charts this patch added to the mix, as <c>/api/v2/versions</c> names it. Several:
    ///     a comma list, or repeat the parameter. A chart carried over from an earlier mix belongs
    ///     to the mix's launch version.
    /// </param>
    /// <param name="addedBy">Only charts that entered this mix in this patch or earlier — everything a cab on that version has.</param>
    /// <param name="addedAfterVersion">Only charts that entered this mix strictly after this patch — what that cab is missing.</param>
    /// <param name="addedAfter">
    ///     Only charts whose patch shipped after this date, exclusive. A patch with no known date is
    ///     never after anything, so it is skipped; use the version forms for legacy mixes.
    /// </param>
    /// <param name="debutedIn">
    ///     Only charts that first appeared anywhere in this patch of this mix — what the patch
    ///     introduced, not what it carried over. A comma list or a repeated parameter, like
    ///     <c>addedInVersion</c>; equal to it plus <c>debut=true</c>.
    /// </param>
    /// <param name="debut">
    ///     <c>true</c> keeps charts that first appeared in this mix, <c>false</c> the carry-overs.
    /// </param>
    /// <param name="channels">
    ///     Only charts whose song sits in these channels on this mix, as <c>/api/v2/channels</c>
    ///     names them. Several: a comma list, or repeat the parameter. A song with no known channel
    ///     never matches, and a channel the mix does not offer is a 400.
    /// </param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<ChartV2Dto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "level")] int? level = null,
        [FromQuery(Name = "type")] string? typeValue = null,
        [FromQuery(Name = "addedInVersion")] string[]? addedIn = null,
        [FromQuery(Name = "addedByVersion")] string? addedBy = null,
        [FromQuery(Name = "addedAfterVersion")] string? addedAfterVersion = null,
        [FromQuery(Name = "addedAfter")] DateOnly? addedAfter = null,
        [FromQuery(Name = "debutedInVersion")] string[]? debutedIn = null,
        [FromQuery(Name = "debut")] bool? debut = null,
        [FromQuery(Name = "channel")] string[]? channels = null,
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

        var (versions, debuts, versionProblem) = await ResolveVersions(mix, addedIn, addedBy, addedAfterVersion, addedAfter, debutedIn);
        if (versionProblem is not null) return versionProblem;
        var (channelPicks, channelProblem) = await ChannelPicks.Resolve(_mediator, mix, channels, (type, title, detail) => Problem(type, title, detail: detail));
        if (channelProblem is not null) return channelProblem;

        var fingerprint = ContinuationToken.FingerprintOf(mix, level, type, pageSize,
            VersionFingerprint(addedIn, addedBy, addedAfterVersion, addedAfter, debutedIn, debut),
            ChannelPicks.Fingerprint(channels));
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var charts = (await _mediator.Send(new GetChartsQuery(mix,
                level is null ? null : DifficultyLevel.From(level.Value), type)))
            .Where(c => InVersions(c, versions, debuts, debut))
            .Where(c => ChannelPicks.Matches(c, channelPicks))
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
    /// <param name="chartId">A chart id from <c>/api/v2/charts</c>.</param>
    [HttpGet("{chartId:guid}/skills")]
    [ProducesResponseType(typeof(ChartSkillProfileDto), StatusCodes.Status200OK, "application/json")]
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
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="level">Optional difficulty level filter.</param>
    /// <param name="typeValue">Optional chart type filter: Single, Double, CoOp, SinglePerformance, DoublePerformance.</param>
    /// <param name="addedIn">
    ///     Only charts this patch added to the mix, as <c>/api/v2/versions</c> names it. Several:
    ///     a comma list, or repeat the parameter. A chart carried over from an earlier mix belongs
    ///     to the mix's launch version.
    /// </param>
    /// <param name="addedBy">Only charts that entered this mix in this patch or earlier — everything a cab on that version has.</param>
    /// <param name="addedAfterVersion">Only charts that entered this mix strictly after this patch — what that cab is missing.</param>
    /// <param name="addedAfter">
    ///     Only charts whose patch shipped after this date, exclusive. A patch with no known date is
    ///     never after anything, so it is skipped; use the version forms for legacy mixes.
    /// </param>
    /// <param name="debutedIn">
    ///     Only charts that first appeared anywhere in this patch of this mix — what the patch
    ///     introduced, not what it carried over. A comma list or a repeated parameter, like
    ///     <c>addedInVersion</c>; equal to it plus <c>debut=true</c>.
    /// </param>
    /// <param name="debut">
    ///     <c>true</c> keeps charts that first appeared in this mix, <c>false</c> the carry-overs.
    /// </param>
    /// <param name="channels">
    ///     Only charts whose song sits in these channels on this mix, as <c>/api/v2/channels</c>
    ///     names them. Several: a comma list, or repeat the parameter. A song with no known channel
    ///     never matches, and a channel the mix does not offer is a 400.
    /// </param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    // Written out rather than a see cref: Swashbuckle renders a cref as its display name, and for a
    // method that is the whole signature — the reader would get System.Nullable{System.Int32} in
    // the endpoint description. XML doc reaches Swagger; a line comment does not, which is why the
    // rationale lives down here.
    [HttpGet("skills")]
    [ProducesResponseType(typeof(CursorPageDto<ChartSkillProfileDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSkills(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "level")] int? level = null,
        [FromQuery(Name = "type")] string? typeValue = null,
        [FromQuery(Name = "addedInVersion")] string[]? addedIn = null,
        [FromQuery(Name = "addedByVersion")] string? addedBy = null,
        [FromQuery(Name = "addedAfterVersion")] string? addedAfterVersion = null,
        [FromQuery(Name = "addedAfter")] DateOnly? addedAfter = null,
        [FromQuery(Name = "debutedInVersion")] string[]? debutedIn = null,
        [FromQuery(Name = "debut")] bool? debut = null,
        [FromQuery(Name = "channel")] string[]? channels = null,
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

        var (versions, debuts, versionProblem) = await ResolveVersions(mix, addedIn, addedBy, addedAfterVersion, addedAfter, debutedIn);
        if (versionProblem is not null) return versionProblem;
        var (channelPicks, channelProblem) = await ChannelPicks.Resolve(_mediator, mix, channels, (type, title, detail) => Problem(type, title, detail: detail));
        if (channelProblem is not null) return channelProblem;

        var fingerprint = ContinuationToken.FingerprintOf(mix, level, type, pageSize,
            VersionFingerprint(addedIn, addedBy, addedAfterVersion, addedAfter, debutedIn, debut),
            ChannelPicks.Fingerprint(channels));
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var chartIds = (await _mediator.Send(new GetChartsQuery(mix,
                level is null ? null : DifficultyLevel.From(level.Value), type)))
            .Where(c => InVersions(c, versions, debuts, debut))
            .Where(c => ChannelPicks.Matches(c, channelPicks))
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
    /// <param name="chartId">A chart id from <c>/api/v2/charts</c>.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    [HttpGet("{chartId:guid}")]
    [ProducesResponseType(typeof(ChartV2Dto), StatusCodes.Status200OK, "application/json")]
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
    /// <param name="chartId">The chart to compare against, from <c>/api/v2/charts</c>.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="minLevel">Only compare charts at or above this level.</param>
    /// <param name="maxLevel">Only compare charts at or below this level.</param>
    /// <param name="minScoringLevel">Only compare charts whose scoring level is at or above this.</param>
    /// <param name="maxScoringLevel">Only compare charts whose scoring level is at or below this.</param>
    /// <param name="minBpm">Only compare charts whose song reaches at least this BPM.</param>
    /// <param name="maxBpm">Only compare charts whose song stays at or below this BPM.</param>
    /// <param name="minNps">Only compare charts with at least this many notes per second, as PIU Center measures it.</param>
    /// <param name="maxNps">Only compare charts with at most this many notes per second, as PIU Center measures it.</param>
    // Filters narrow what the anchor is compared against and the scores are recomputed; they never
    // sieve a precalculated list, which would return nothing for any filter narrow enough to be
    // interesting. That also makes this the out-of-window path — what D23s play like a D18 is a real
    // question and deliberately outside the ±1 the nightly job precalculates. See
    // docs/design/chart-similarity.md; a line comment keeps it out of the Swagger description.
    [HttpGet("{chartId:guid}/similar")]
    [ProducesResponseType(typeof(SimilarChartsDto), StatusCodes.Status200OK, "application/json")]
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
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="count">How many charts to draw. Defaults to 5.</param>
    /// <param name="chartTypes">Which chart types may be drawn: Single, Double, CoOp. Repeat the parameter for more than one.</param>
    /// <param name="songTypes">Which song cuts may be drawn: Arcade, ShortCut, FullSong, Remix. Repeat the parameter for more than one.</param>
    /// <param name="minLevel">The lowest level to draw from.</param>
    /// <param name="maxLevel">The highest level to draw from.</param>
    /// <param name="buckets">
    ///     Minimum pull counts. "Single:2" pulls at least 2 singles; "S19:8" at least 8 S19s;
    ///     "S21,S22,D23:4" at least 4 from that set of folders. Bucket minimums win over
    ///     <paramref name="count" /> when they exceed it.
    /// </param>
    /// <param name="addedIn">
    ///     Only charts this patch added to the mix, as <c>/api/v2/versions</c> names it. Several:
    ///     a comma list, or repeat the parameter. A chart carried over from an earlier mix belongs
    ///     to the mix's launch version.
    /// </param>
    /// <param name="addedBy">Only charts that entered this mix in this patch or earlier — everything a cab on that version has.</param>
    /// <param name="addedAfterVersion">Only charts that entered this mix strictly after this patch — what that cab is missing.</param>
    /// <param name="addedAfter">
    ///     Only charts whose patch shipped after this date, exclusive. A patch with no known date is
    ///     never after anything, so it is skipped; use the version forms for legacy mixes.
    /// </param>
    /// <param name="debutedIn">
    ///     Only charts that first appeared anywhere in this patch of this mix — what the patch
    ///     introduced, not what it carried over. A comma list or a repeated parameter, like
    ///     <c>addedInVersion</c>; equal to it plus <c>debut=true</c>.
    /// </param>
    /// <param name="debut">
    ///     <c>true</c> keeps charts that first appeared in this mix, <c>false</c> the carry-overs.
    /// </param>
    /// <param name="channels">
    ///     Only charts whose song sits in these channels on this mix, as <c>/api/v2/channels</c>
    ///     names them. Several: a comma list, or repeat the parameter. A song with no known channel
    ///     never matches, and a channel the mix does not offer is a 400.
    /// </param>
    [HttpGet("random")]
    [ProducesResponseType(typeof(CursorPageDto<ChartV2Dto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRandom(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "count")] int count = 5,
        [FromQuery(Name = "chartTypes")] string[]? chartTypes = null,
        [FromQuery(Name = "songTypes")] string[]? songTypes = null,
        [FromQuery(Name = "minLevel")] int? minLevel = null,
        [FromQuery(Name = "maxLevel")] int? maxLevel = null,
        [FromQuery(Name = "bucket")] string[]? buckets = null,
        [FromQuery(Name = "addedInVersion")] string[]? addedIn = null,
        [FromQuery(Name = "addedByVersion")] string? addedBy = null,
        [FromQuery(Name = "addedAfterVersion")] string? addedAfterVersion = null,
        [FromQuery(Name = "addedAfter")] DateOnly? addedAfter = null,
        [FromQuery(Name = "debutedInVersion")] string[]? debutedIn = null,
        [FromQuery(Name = "debut")] bool? debut = null,
        [FromQuery(Name = "channel")] string[]? channels = null)
    {
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();
        if (count < 1) return Problem("invalid-count", "count must be at least 1.");

        var (versions, debuts, versionProblem) = await ResolveVersions(mix, addedIn, addedBy, addedAfterVersion, addedAfter, debutedIn);
        if (versionProblem is not null) return versionProblem;
        var (channelPicks, channelProblem) = await ChannelPicks.Resolve(_mediator, mix, channels, (type, title, detail) => Problem(type, title, detail: detail));
        if (channelProblem is not null) return channelProblem;
        // The debut names narrow the added-in picks: a debut in 1.01.0 is a chart added in 1.01.0
        // whose origin mix is this one, so the draw takes the intersection and the flag.
        IReadOnlySet<string>? picked = versions;
        if (debuts is not null)
            picked = versions is null ? debuts : versions.Intersect(debuts).ToHashSet(StringComparer.Ordinal);
        // A version filter that reaches no patch is an empty draw, not an unfiltered one: the
        // randomizer reads an empty set as "every version". So is a filter that contradicts itself.
        if (picked is { Count: 0 } || (debuts is not null && debut == false))
            return Json(Page(Array.Empty<ChartV2Dto>(), 0, 0, null));

        var settings = new RandomSettings { Count = count };
        if (picked is not null) settings.Versions = picked.ToHashSet(StringComparer.Ordinal);
        if (channelPicks is not null) settings.Channels = channelPicks.ToHashSet();
        // The debut pair stays a pair: the names imply the flag, and a bare debut= sets it. Anything
        // between these two lines makes the else hang off the wrong condition.
        if (debuts is not null) settings.Debut = true;
        else if (debut is not null) settings.Debut = debut;

        // Unasked, a draw covers every type the mix has except co-op, whose "level" is a player
        // count and whose weights are a separate bucket. On RISE that means half-doubles.
        var types = chartTypes is null
            ? MixProfiles.For(mix).ChartTypes.Where(t => t.Category() != ChartTypeCategory.CoOp).ToArray()
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

        // Weights live in one bucket per folder, so the asked-for types are folded into folders
        // before they are set: a caller asking only for half-doubles is asking for the doubles
        // bucket, and matching ChartType.Double exactly would leave every weight at zero and
        // draw nothing (docs/design/rise.md §3.1).
        var weightBuckets = types.Select(t => t.Category()).ToHashSet();
        for (var level = minLevel ?? DifficultyLevel.Min; level <= (maxLevel ?? DifficultyLevel.Max); level++)
        {
            if (weightBuckets.Contains(ChartTypeCategory.Single)) settings.LevelWeights[level] = 1;
            if (weightBuckets.Contains(ChartTypeCategory.Double)) settings.DoubleLevelWeights[level] = 1;
            if (weightBuckets.Contains(ChartTypeCategory.CoOp) && level <= 5) settings.PlayerCountWeights[level] = 1;
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

    /// <summary>
    ///     The four version parameters, resolved into the names a chart's added-in patch must be in — null
    ///     when none was asked — or a problem when one names a patch the mix does not have. One
    ///     resolver for the three chart reads, so they cannot disagree about "through 2.09.0".
    /// </summary>
    private async Task<(IReadOnlySet<string>? Names, IReadOnlySet<string>? DebutNames, ObjectResult? Problem)>
        ResolveVersions(MixEnum mix, string[]? addedIn, string? addedBy, string? addedAfterVersion, DateOnly? addedAfter,
            string[]? debutedIn)
    {
        var inVersions = SplitPicks(addedIn);
        var debutVersions = SplitPicks(debutedIn);
        var addedAsked = inVersions is { Length: > 0 } || addedBy is not null || addedAfterVersion is not null || addedAfter is not null;
        var debutAsked = debutVersions is { Length: > 0 };
        if (!addedAsked && !debutAsked) return (null, null, null);

        var versions = await _mediator.Send(new GetMixVersionsQuery(mix));
        IReadOnlySet<string>? names = null;
        if (addedAsked &&
            !MixVersionRange.TryResolve(versions, inVersions, addedBy, addedAfterVersion, addedAfter, out names, out var unknown))
            return (null, null, UnknownVersion(mix, unknown));

        IReadOnlySet<string>? debutNames = null;
        // The debut names resolve through the same table: a debut is an added-in patch of this mix.
        if (debutAsked &&
            !MixVersionRange.TryResolve(versions, debutVersions, null, null, null, out debutNames, out var unknownDebut))
            return (null, null, UnknownVersion(mix, unknownDebut));

        return (names, debutNames, null);
    }

    private static string[]? SplitPicks(string[]? picks)
    {
        return picks?
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
    }

    private ObjectResult UnknownVersion(MixEnum mix, string? unknown)
    {
        return Problem("invalid-version", $"'{unknown}' is not a version of this mix.",
            detail: $"The versions of {mix} are listed by /api/v2/versions?mix={mix}.");
    }

    /// <summary>
    ///     The row filter behind every version parameter: the added-in names are the mix's own
    ///     timeline, the debut names and the flag are first appearance anywhere. A chart with no
    ///     known patch never matches a name; the flag reads the origin mix and needs no patch.
    /// </summary>
    private static bool InVersions(Chart chart, IReadOnlySet<string>? names, IReadOnlySet<string>? debutNames, bool? debut)
    {
        if (debut is not null && chart.IsDebut != debut.Value) return false;
        if (debutNames is not null &&
            !(chart.IsDebut && chart.AddedIn is not null && debutNames.Contains(chart.AddedIn.Version)))
            return false;
        return names is null || (chart.AddedIn is not null && names.Contains(chart.AddedIn.Version));
    }

    private static string VersionFingerprint(string[]? addedIn, string? addedBy, string? addedAfterVersion,
        DateOnly? addedAfter, string[]? debutedIn, bool? debut)
    {
        var picks = addedIn is null ? string.Empty : string.Join(",", addedIn.OrderBy(v => v, StringComparer.Ordinal));
        var debuts = debutedIn is null ? string.Empty : string.Join(",", debutedIn.OrderBy(v => v, StringComparer.Ordinal));
        return $"{picks}|{addedBy}|{addedAfterVersion}|{addedAfter:yyyy-MM-dd}|{debuts}|{debut}";
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
