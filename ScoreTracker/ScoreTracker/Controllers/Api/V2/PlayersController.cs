using MediatR;
using Microsoft.AspNetCore.Mvc;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     A player's own record: profile, scores, sessions and per-attempt journal.
///     <para>
///         <c>me</c> is the only reachable player until share-gating lands; a personal token resolves
///         to its own user and nothing else.
///     </para>
/// </summary>
[ApiToken]
[Route(RoutePrefix + "/players")]
public sealed class PlayersController : ApiV2ControllerBase
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;

    public PlayersController(IMediator mediator, ICurrentUserAccessor currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    private static string ScoringModel(MixEnum mix)
    {
        return mix.UsesLegacyScoring() ? "legacy" : "phoenix";
    }

    /// <summary>
    ///     Resolves the route's player id. "me" is the caller; anything else is refused until a
    ///     credential exists that can legitimately name another player.
    /// </summary>
    private bool TryResolvePlayer(string playerId, out Guid userId, out ObjectResult? failure)
    {
        userId = _currentUser.User.Id;
        failure = null;
        if (string.Equals(playerId, "me", StringComparison.OrdinalIgnoreCase)) return true;
        if (Guid.TryParse(playerId, out var requested) && requested == userId) return true;

        // 404, not 403: a 403 would confirm the player exists.
        failure = NotFoundProblem("No player with that id is readable with this credential.");
        return false;
    }

    /// <summary>The player's profile, with their most recently observed in-game tag.</summary>
    [HttpGet("{playerId}")]
    public async Task<IActionResult> GetPlayer([FromRoute] string playerId)
    {
        if (!TryResolvePlayer(playerId, out var userId, out var failure)) return failure!;

        var user = await _mediator.Send(new GetUserByIdQuery(userId));
        if (user is null) return NotFoundProblem("No player with that id is readable with this credential.");

        var (tag, seenAt) = await ResolveGameTag(userId);
        return Json(new PlayerV2Dto(user, tag, seenAt));
    }

    /// <summary>
    ///     Best attempts in one mix.
    /// </summary>
    /// <param name="recordedAfter">
    ///     Only records written after this instant. The incremental-sync parameter — with it a tool
    ///     stays current without webhooks and without re-reading a player's whole history.
    /// </param>
    [HttpGet("{playerId}/scores")]
    public async Task<IActionResult> GetScores([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "minLevel")] int? minLevel = null,
        [FromQuery(Name = "maxLevel")] int? maxLevel = null,
        [FromQuery(Name = "chartType")] string? chartTypeValue = null,
        [FromQuery(Name = "isBroken")] bool? isBroken = null,
        [FromQuery(Name = "recordedAfter")] DateTimeOffset? recordedAfter = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryResolvePlayer(playerId, out var userId, out var failure)) return failure!;
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var mixFailure))
            return mixFailure!;

        ChartType? chartType = null;
        if (chartTypeValue is not null)
        {
            if (!Enum.TryParse<ChartType>(chartTypeValue, true, out var parsed))
                return Problem("invalid-chart-type", "The chartType parameter is not a chart type.",
                    detail: $"Valid values: {string.Join(", ", Enum.GetNames<ChartType>())}");
            chartType = parsed;
        }

        var fingerprint = ContinuationToken.FingerprintOf(userId, mix, minLevel, maxLevel, chartType,
            isBroken, recordedAfter, pageSize);
        ContinuationToken? from = null;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            from = token;
        }

        var charts = (await _mediator.Send(new GetChartsQuery(mix))).ToDictionary(c => c.Id);
        // Only the Phoenix mixes have a PUMBILITY formula — asking for one on a legacy mix throws.
        // Legacy rows report a null rating rather than a fabricated number.
        var scoring = mix.UsesLegacyScoring() ? null : ScoringConfiguration.PumbilityScoring(mix, true);

        var rows = (await _mediator.Send(new GetPhoenixRecordsQuery(userId, mix)))
            .Where(r => charts.ContainsKey(r.ChartId))
            .Where(r => recordedAfter is null || r.RecordedDate > recordedAfter.Value)
            .Where(r => isBroken is null || r.IsBroken == isBroken.Value)
            .Where(r => minLevel is null || (int)charts[r.ChartId].Level >= minLevel.Value)
            .Where(r => maxLevel is null || (int)charts[r.ChartId].Level <= maxLevel.Value)
            .Where(r => chartType is null || charts[r.ChartId].Type == chartType.Value)
            // Newest first, chart id as tiebreaker, matching the keyset the cursor carries.
            .OrderByDescending(r => r.RecordedDate)
            .ThenByDescending(r => r.ChartId)
            .ToArray();

        if (from?.Key is not null && DateTimeOffset.TryParse(from.Value.Key, out var afterDate))
            rows = rows.Where(r => r.RecordedDate < afterDate
                                   || (r.RecordedDate == afterDate && r.ChartId.CompareTo(from.Value.Id!.Value) < 0))
                .ToArray();

        var page = rows.Take(pageSize).ToArray();
        var next = rows.Length > page.Length
            ? ContinuationToken.FromKeyset(page[^1].RecordedDate.ToString("O"), page[^1].ChartId, fingerprint)
            : (ContinuationToken?)null;

        return Json(new PlayerScorePageDto
        {
            Mix = mix.ToString(),
            ScoringModel = ScoringModel(mix),
            Limit = pageSize,
            Data = page.Select(r => new PlayerScoreDto(r, mix,
                scoring is null || r.Score is null
                    ? null
                    : Math.Round(scoring.GetScore(charts[r.ChartId], r.Score.Value,
                        r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken), 2))).ToArray(),
            Next = next is null ? null : NextUrlFor(next.Value)
        });
    }

    /// <summary>Import and play sessions, newest first.</summary>
    [HttpGet("{playerId}/sessions")]
    public async Task<IActionResult> GetSessions([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryResolvePlayer(playerId, out var userId, out var failure)) return failure!;
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var mixFailure))
            return mixFailure!;

        var fingerprint = ContinuationToken.FingerprintOf(userId, mix, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        // The session read pages across every mix, so the page is filtered after the fact; the
        // groups are few enough per player that reading a generous window and filtering is honest.
        var sessions = await _mediator.Send(new GetRecentSessionsQuery(userId, 1, MaxLimit));
        var filtered = sessions.Groups.Where(g => g.Mix == mix).ToArray();
        var rows = filtered.Skip(offset).Take(pageSize).Select(g => new SessionDto(g)).ToArray();
        var next = offset + rows.Length < filtered.Length
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return Json(Page(rows, pageSize, filtered.Length, next));
    }

    /// <summary>
    ///     Every play, not just the ones that became records — the per-attempt history, with judgment
    ///     counts where the source carried them.
    /// </summary>
    [HttpGet("{playerId}/journal")]
    public async Task<IActionResult> GetJournal([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "since")] DateTimeOffset? since = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryResolvePlayer(playerId, out var userId, out var failure)) return failure!;
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var mixFailure))
            return mixFailure!;

        var fingerprint = ContinuationToken.FingerprintOf(userId, mix, since, pageSize);
        DateTimeOffset? beforeOccurredAt = null;
        Guid? beforeChartId = null;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            if (token.Key is not null && DateTimeOffset.TryParse(token.Key, out var parsed))
            {
                beforeOccurredAt = parsed;
                beforeChartId = token.Id;
            }
        }

        // One more than asked for, so "is there a next page" needs no count query.
        var entries = await _mediator.Send(new GetPlayerJournalQuery(userId, mix, beforeOccurredAt,
            beforeChartId, since, pageSize + 1));

        var page = entries.Take(pageSize).ToArray();
        var next = entries.Count > page.Length
            ? ContinuationToken.FromKeyset(page[^1].OccurredAt.ToString("O"), page[^1].ChartId, fingerprint)
            : (ContinuationToken?)null;

        return Json(Page(page.Select(e => new JournalEntryDto(e, mix)).ToArray(), pageSize, null, next));
    }

    /// <summary>
    ///     The tag from the most recent mix link. One value rather than one per mix: the tag is an
    ///     AM Pass account setting shared across the Phoenix mixes, and the per-mix rows are snapshots
    ///     taken by scrapes that ran on different days, not distinct identities.
    /// </summary>
    private async Task<(string? Tag, DateTimeOffset? SeenAt)> ResolveGameTag(Guid userId)
    {
        foreach (var mix in new[] { MixEnum.Phoenix2, MixEnum.Phoenix })
        {
            var tag = await _mediator.Send(new GetLinkedOfficialPlayerTagQuery(mix, userId));
            if (!string.IsNullOrWhiteSpace(tag)) return (tag, null);
        }

        return (null, null);
    }
}
