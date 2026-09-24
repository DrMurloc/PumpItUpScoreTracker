using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Dtos.ApiV2;
using ScoreTracker.Web.Security;
using ScoreTracker.Web.Services;

namespace ScoreTracker.Web.Controllers.Api.V2;

/// <summary>
///     A player's own record: profile, scores, sessions and per-attempt journal.
///     <para>
///         <c>me</c> is the only reachable player until share-gating lands; a personal token resolves
///         to its own user and nothing else.
///     </para>
/// </summary>
[ApiV2]
[EnableRateLimiting(ApiV2RateLimiting.PolicyName)]
[Route(RoutePrefix + "/players")]
public sealed class PlayersController : ApiV2ControllerBase
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _clock;
    private readonly IMediator _mediator;

    public PlayersController(IMediator mediator, ICurrentUserAccessor currentUser, IDateTimeOffsetAccessor clock)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    ///     Resolves the route's player id against the caller's credential.
    ///     <para>
    ///         A personal token reaches only its own user. A tool key reaches every player who
    ///         granted it access, and cannot use "me" — a tool is not a player.
    ///     </para>
    ///     <para>
    ///         An unreachable player is 404, never 403. A 403 would confirm the player exists, which
    ///         turns this endpoint into an account-enumeration oracle.
    ///     </para>
    /// </summary>
    public const int MaxPlaysPerRequest = 100;
    public const int MaxSourceLength = 32;

    // ScoreScreen's formula matches the game to ±1 (it floors where the machine sometimes does
    // not), so a play one point off still reconciles; two points off is a misread.
    private const int ChecksumTolerance = 1;

    /// <summary>
    ///     <c>POST me/plays</c> — plays a tool observed for the caller, the one write on v2
    ///     (docs/design/rise.md §6.3): built for the RISE capture app and open to any tool acting
    ///     with a player's own token. The judgments are optional. A play that carries them has its
    ///     score recomputed from its five counts and max combo, and one that does not reconcile
    ///     refuses the whole request — the checksum that keeps a misread screen out of a record; its
    ///     award is derived from the same counts, and a claimed one has to agree. A play without
    ///     them is taken as read, score and claimed award alike, as long as the two agree. A play
    ///     that passes goes through the ledger's best-attempt policy, so it becomes the record only
    ///     where it beats it (a break only where the player seats breaks), and into the journal
    ///     either way, dated by the play time the tool observed, all of one request under one session.
    /// </summary>
    [HttpPost("me/plays")]
    [ProducesResponseType(typeof(RecordPlaysResultDto), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordPlays([FromBody] RecordPlaysRequestDto? body)
    {
        if (User.ToolId() is not null)
            return Problem("tool-has-no-self", "A tool has no 'me'.",
                detail: "Plays are recorded with the player's own token.");
        if (body is null) return Problem("body-required", "A request body is required.");
        if (!V2MixParser.TryParse(body.Mix, out var mix)) return MixRequiredProblem();
        if (RequestProblem(body, mix) is { } requestProblem) return requestProblem;

        var charts = (await _mediator.Send(new GetChartsQuery(mix))).ToDictionary(c => c.Id);
        var now = _clock.Now;
        var resolved = new List<ResolvedPlay>();
        // The count was bounded above; Take keeps the bound visible to a reader as well as an analyzer.
        foreach (var play in body.Plays!.Take(MaxPlaysPerRequest))
        {
            var (resolvedPlay, problem) = ResolvePlay(resolved.Count, play, mix, charts, now);
            if (problem != null) return problem;
            resolved.Add(resolvedPlay!);
        }

        // Whether a break on a chart the player has never passed is seated as their best: the
        // tool's own choice, else the mix's default — the same default the import page reads.
        var recordBrokenAsBest = body.RecordBrokenAsBest ?? BrokenScorePreference.DefaultFor(mix);
        await Record(mix, "api:" + body.Source, recordBrokenAsBest, resolved);
        return Ok(new RecordPlaysResultDto(resolved.Count, mix.ToString(), ScoringModelOf(mix)));
    }

    private sealed record ResolvedPlay(Chart Chart, ObservedPlayDto Play, PhoenixScore Score, PhoenixPlate? Award,
        JudgementCounts? Judgements);

    private ObjectResult? RequestProblem(RecordPlaysRequestDto body, MixEnum mix)
    {
        if (mix.UsesLegacyScoring())
            return Problem("legacy-mix", "This mix is not Phoenix-scored.",
                detail: $"{mix.GetName()} keeps a letter grade, not five judgments and a 1,000,000-point score.");
        if (string.IsNullOrWhiteSpace(body.Source) || body.Source.Length > MaxSourceLength ||
            !body.Source.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            return Problem("source-required", "The source names the tool that observed the plays.",
                detail: $"One to {MaxSourceLength} letters, digits, '.', '_' or '-'.");
        if (body.Plays is null || body.Plays.Count == 0 || body.Plays.Count > MaxPlaysPerRequest)
            return Problem("plays-required", $"Between one and {MaxPlaysPerRequest} plays per request.");
        return null;
    }

    private (ResolvedPlay? Play, ObjectResult? Problem) ResolvePlay(int index, ObservedPlayDto play, MixEnum mix,
        IReadOnlyDictionary<Guid, Chart> charts, DateTimeOffset now)
    {
        var chart = ResolveChart(charts, play);
        if (chart == null) return (null, NotFoundProblem($"Play {index}: no chart matches on {mix.GetName()}."));
        var (judgements, judgementsProblem) = ReadJudgements(index, play);
        if (judgementsProblem != null) return (null, judgementsProblem);
        if (!PhoenixScore.TryParse(play.Score, out var score))
            return (null, Problem("score-invalid", $"Play {index}: the score is not a Phoenix score."));
        // The play time is the journal's key and the record's date: an omitted one would file
        // the play in year 1, a future one ahead of everything the player does next.
        if (play.PlayedAt == default || play.PlayedAt > now.AddMinutes(5))
            return (null, Problem("played-at-invalid", $"Play {index}: playedAt must be when the play happened.",
                detail: "An omitted or future play time cannot be journaled."));
        if (ScoreProblem(index, play, judgements) is { } scoreProblem) return (null, scoreProblem);
        var (award, awardProblem) = ResolveAward(index, play, score, judgements, mix);
        if (awardProblem != null) return (null, awardProblem);
        return (new ResolvedPlay(chart, play, score, award, judgements), null);
    }

    /// <summary>
    ///     The five counts and the max combo, read as one set: all six sent, or none of them for a
    ///     play whose judgments were never read. Anything in between is refused rather than half-used.
    /// </summary>
    private (JudgementCounts? Judgements, ObjectResult? Problem) ReadJudgements(int index, ObservedPlayDto play)
    {
        var sent = new[] { play.Perfects, play.Greats, play.Goods, play.Bads, play.Misses, play.MaxCombo };
        if (sent.Any(count => count < 0))
            return (null, Problem("judgments-invalid", $"Play {index}: judgment counts cannot be negative."));
        if (play is { Perfects: { } perfects, Greats: { } greats, Goods: { } goods, Bads: { } bads,
                Misses: { } misses, MaxCombo: { } maxCombo })
            return (new JudgementCounts(perfects, greats, goods, bads, misses, maxCombo), null);
        if (sent.All(count => count == null)) return (null, null);
        return (null, Problem("judgments-incomplete", "The judgments are all six numbers or none of them.",
            detail: $"Play {index}: perfects, greats, goods, bads, misses and maxCombo are sent together " +
                    "or left out together."));
    }

    /// <summary>
    ///     Judgments have to reproduce the score. Without them the score is taken as read, except
    ///     that a pass never scores zero — which is what a score left out of the request reads as.
    /// </summary>
    private ObjectResult? ScoreProblem(int index, ObservedPlayDto play, JudgementCounts? judgements)
    {
        if (judgements is not { MaxCombo: { } maxCombo })
            return !play.IsBroken && play.Score == 0
                ? Problem("score-invalid", $"Play {index}: a pass without judgments scores above zero.")
                : null;

        var screen = new ScoreScreen(judgements.Perfects, judgements.Greats, judgements.Goods, judgements.Bads,
            judgements.Misses, maxCombo);
        // The formula scores a screen it cannot read as zero, so a zero would otherwise match it.
        if (!screen.IsValid)
            return Problem("judgments-do-not-reconcile", "The judgments do not produce the score.",
                detail: $"Play {index}: {Breakdown(judgements)} with max combo {maxCombo} is not a play the " +
                        "formula can score: the counts total 1 to 9,999 notes and the combo stays within them.");
        var expected = (int)screen.CalculatePhoenixScore;
        if (Math.Abs(expected - play.Score) <= ChecksumTolerance) return null;
        return Problem("judgments-do-not-reconcile", "The judgments do not produce the score.",
            detail: $"Play {index}: {Breakdown(judgements)} with max combo {maxCombo} scores {expected:N0}, " +
                    $"not {play.Score:N0}.");
    }

    /// <summary>
    ///     The award a play carries. With judgments it is derived from them
    ///     (<see cref="PhoenixPlateHelperMethods.Tolerances" /> is the rule) and a caller's claim only
    ///     has to agree; without them a claim stands unless the score contradicts it. A break carries
    ///     none.
    /// </summary>
    private (PhoenixPlate? Award, ObjectResult? Problem) ResolveAward(int index, ObservedPlayDto play,
        PhoenixScore score, JudgementCounts? judgements, MixEnum mix)
    {
        if (play.IsBroken) return (null, null);
        var awards = mix.AwardsOf();
        PhoenixPlate? claimed = null;
        if (!string.IsNullOrWhiteSpace(play.Award))
        {
            claimed = AwardSets.TryParseShorthand(play.Award, mix);
            if (claimed == null)
                return (null, Problem("award-invalid", $"Play {index}: not an award {mix.GetName()} hands out.",
                    detail: "Valid values: " + string.Join(", ", awards.Select(a => a.GetShorthand(mix)))));
        }

        if (judgements == null) return AwardFromScore(index, score, claimed, awards, mix);
        var earned = DeriveAward(awards, judgements);
        if (claimed == null || claimed == earned) return (earned, null);
        return (null, Problem("award-does-not-reconcile", "The judgments do not earn the award.",
            detail: $"Play {index}: {Breakdown(judgements)} earns " +
                    $"{(earned == null ? "no award" : earned.Value.GetShorthand(mix))}, " +
                    $"not {claimed.Value.GetShorthand(mix)}."));
    }

    /// <summary>
    ///     A play without judgments keeps the award it was sent with, as long as the score agrees:
    ///     only 1,000,000 is a Perfect Game, and a million sent with no award is one.
    /// </summary>
    private (PhoenixPlate? Award, ObjectResult? Problem) AwardFromScore(int index, PhoenixScore score,
        PhoenixPlate? claimed, IReadOnlyList<PhoenixPlate> awards, MixEnum mix)
    {
        var isPerfect = score == PhoenixScore.Max;
        if (claimed == null)
            return (isPerfect && awards.Contains(PhoenixPlate.PerfectGame) ? PhoenixPlate.PerfectGame : null, null);
        var claimsPerfect = claimed == PhoenixPlate.PerfectGame;
        if (claimsPerfect == isPerfect) return (claimed, null);
        return (null, Problem("award-does-not-reconcile", "The score does not earn the award.",
            detail: isPerfect
                ? $"Play {index}: 1,000,000 is a Perfect Game, not {claimed.Value.GetShorthand(mix)}."
                : $"Play {index}: {(int)score:N0} is not a Perfect Game; only 1,000,000 is."));
    }

    private static string Breakdown(JudgementCounts judgements)
    {
        return $"{judgements.Perfects}/{judgements.Greats}/{judgements.Goods}/{judgements.Bads}/{judgements.Misses}";
    }

    /// <summary>
    ///     One session for everything a request saw, so the record path and the journal file the
    ///     plays under the same sitting. The record first — keep-best, so only a play that beats
    ///     it moves it, dated by the play; a break only where the player seats breaks — then the
    ///     journal, which is idempotent on play time and so never journals a record's play twice.
    /// </summary>
    private async Task Record(MixEnum mix, string source, bool recordBrokenAsBest, IReadOnlyList<ResolvedPlay> plays)
    {
        var sessionId = Guid.NewGuid();
        foreach (var r in plays.Where(r => !r.Play.IsBroken || recordBrokenAsBest))
            await _mediator.Send(new UpdatePhoenixBestAttemptCommand(r.Chart.Id, r.Play.IsBroken, r.Score, r.Award,
                KeepBestStats: true, Source: source, Mix: mix, SessionId: sessionId, RecordedAt: r.Play.PlayedAt,
                Judgements: r.Judgements));
        await _mediator.Send(new RecordObservedPlaysCommand(_currentUser.User.Id, mix, source, sessionId,
            plays.Select(r => new RecordObservedPlaysCommand.ObservedPlay(r.Chart.Id, r.Score, r.Award,
                r.Play.IsBroken, r.Play.PlayedAt, r.Judgements)).ToArray(), IncludeBroken: recordBrokenAsBest));
    }

    /// <summary>
    ///     The award a run earned on this mix: the strictest plate rule it satisfies that the mix
    ///     hands out. A plate mix falls through to Rough Game; a mark mix (Rise) to no mark.
    /// </summary>
    private static PhoenixPlate? DeriveAward(IReadOnlyList<PhoenixPlate> awards, JudgementCounts judgements)
    {
        foreach (var rule in PhoenixPlateHelperMethods.Tolerances)
            if (awards.Contains(rule.Plate) &&
                rule.Tolerates(judgements.Greats, judgements.Goods, judgements.Bads, judgements.Misses))
                return rule.Plate;
        return awards.Contains(PhoenixPlate.RoughGame) ? PhoenixPlate.RoughGame : null;
    }

    private static Chart? ResolveChart(IReadOnlyDictionary<Guid, Chart> charts, ObservedPlayDto play)
    {
        if (play.ChartId is { } id) return charts.GetValueOrDefault(id);
        if (string.IsNullOrWhiteSpace(play.SongName) || play.Level is null ||
            !Enum.TryParse<ChartType>(play.ChartType, true, out var type)) return null;
        return charts.Values.FirstOrDefault(c => c.Type == type && (int)c.Level == play.Level &&
                                                 string.Equals(c.Song.Name.ToString(), play.SongName.Trim(),
                                                     StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(Guid? UserId, ObjectResult? Failure)> ResolvePlayer(string playerId)
    {
        var toolId = User.ToolId();
        var isMe = string.Equals(playerId, "me", StringComparison.OrdinalIgnoreCase);

        if (toolId is null)
        {
            var self = _currentUser.User.Id;
            if (isMe) return (self, null);
            if (Guid.TryParse(playerId, out var requested) && requested == self) return (self, null);
            return (null, Unreachable());
        }

        if (isMe)
            return (null, Problem("tool-has-no-self", "A tool has no 'me'.",
                detail: "Address a player by id. GET /api/v2/players lists the ones that shared with you."));

        if (!Guid.TryParse(playerId, out var target)) return (null, Unreachable());

        return await _mediator.Send(new CanToolReadPlayerQuery(toolId.Value, target))
            ? (target, null)
            : (null, Unreachable());
    }

    private ObjectResult Unreachable()
    {
        return NotFoundProblem("No player with that id is readable with this credential.");
    }

    /// <summary>The most chart ids one scores request may name.</summary>
    public const int MaxChartIds = 50;

    /// <summary>
    ///     A comma-separated chart-id list, or null when the parameter was not sent. Blank entries
    ///     are ignored so a trailing comma is not an error; anything else that is not a GUID is,
    ///     and so is a list longer than <see cref="MaxChartIds" /> — a caller who wants more than
    ///     fifty charts wants the unfiltered read.
    /// </summary>
    private bool TryParseChartIds(string? value, out HashSet<Guid>? chartIds, out ObjectResult? failure)
    {
        chartIds = null;
        failure = null;
        if (value is null) return true;

        var ids = new HashSet<Guid>();
        foreach (var token in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Guid.TryParse(token, out var id))
            {
                failure = Problem("invalid-chart-id", "The chartIds parameter is not a list of chart ids.",
                    detail: $"'{token}' is not a chart id. Send chart ids from /api/v2/charts, comma-separated.");
                return false;
            }

            ids.Add(id);
        }

        if (ids.Count > MaxChartIds)
        {
            failure = Problem("too-many-chart-ids", "The chartIds parameter names too many charts.",
                detail: $"At most {MaxChartIds} chart ids per request; for more, read without the filter.");
            return false;
        }

        chartIds = ids;
        return true;
    }

    /// <summary>
    ///     The readable set, narrowed to one community's members when a filter was sent.
    ///     <para>
    ///         The filter can only narrow: it intersects the players the credential may already
    ///         read with the community's members as the viewer may see them. The viewer is the
    ///         tool's maker for a tool key and the caller for a personal token, so a private
    ///         community filters for its members and answers everyone else exactly as an unknown
    ///         name does (docs/design/api-v2-round-2.md §4).
    ///     </para>
    /// </summary>
    /// <returns>The ids, sorted; the normalized community name for the cursor fingerprint; or the failure.</returns>
    private async Task<(IReadOnlyList<Guid>? Ids, string? CommunityKey, ObjectResult? Failure)> ReadableWithin(
        string? community)
    {
        var readable = await ReadablePlayerIds(_mediator, _currentUser);
        if (community is null) return (readable.OrderBy(id => id).ToArray(), null, null);

        Name name;
        try
        {
            name = Name.From(community);
        }
        catch (InvalidNameException)
        {
            return (null, null, Problem("invalid-community", "The community parameter is not a community name.",
                detail: "Send the community's name as the site shows it — World, a country, or a community's own name."));
        }

        var toolId = User.ToolId();
        var viewer = toolId is null
            ? _currentUser.User.Id
            : await _mediator.Send(new GetToolOwnerQuery(toolId.Value));
        var members = await _mediator.Send(new GetCommunityMembersForViewerQuery(name, viewer));
        if (members is null)
            return (null, null, NotFoundProblem("No community by that name is readable with this credential."));

        return (readable.Where(members.Contains).OrderBy(id => id).ToArray(), name.ToString().ToLowerInvariant(), null);
    }

    /// <summary>
    ///     Every player who has shared with the calling tool.
    ///     <para>
    ///         The endpoint the whole sharing model exists to serve: without it a tool has no way to
    ///         learn who consented. A personal token gets a one-row list of itself.
    ///     </para>
    /// </summary>
    /// <param name="community">
    ///     Only members of this community, by the name the site shows — <c>World</c>, a country, or
    ///     a community's own name. Narrows the players you can already read; it never adds one. A
    ///     private community filters only for its members (the tool's maker, or you), and otherwise
    ///     answers 404 exactly as an unknown name does.
    /// </param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageDto<PlayerV2Dto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlayers(
        [FromQuery(Name = "community")] string? community = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var (ids, communityKey, failure) = await ReadableWithin(community);
        if (ids is null) return failure!;
        var userIds = ids;

        var fingerprint = ContinuationToken.FingerprintOf(CredentialKey(_currentUser), pageSize, communityKey);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        var page = userIds.Skip(offset).Take(pageSize).ToArray();
        var rows = new List<PlayerV2Dto>();
        foreach (var id in page)
        {
            var user = await _mediator.Send(new GetUserByIdQuery(id));
            if (user is null) continue;

            rows.Add(new PlayerV2Dto(user, await ResolveGameTag(id)));
        }

        var next = offset + page.Length < userIds.Count
            ? ContinuationToken.FromOffset(offset + page.Length, fingerprint)
            : (ContinuationToken?)null;

        return Json(Page(rows, pageSize, userIds.Count, next));
    }

    /// <summary>
    ///     PUMBILITY is a Phoenix-era number. A legacy mix answers 404 in the same voice as a tier
    ///     list that a mix never published: "this does not exist here" is a different answer from
    ///     "this is empty here", and a caller that cannot tell them apart waits for data that will
    ///     never arrive.
    /// </summary>
    private ObjectResult NoPumbilityProblem(MixEnum mix)
    {
        return NotFoundProblem($"PUMBILITY is a Phoenix-era number; {mix} has no formula for it. " +
                               "Ask for a Phoenix mix.");
    }

    /// <summary>
    ///     PUMBILITY numbers for every readable player in one mix, highest PUMBILITY first — the
    ///     same object <c>/api/v2/players/{playerId}/stats</c> returns, one per row, filtered like
    ///     <c>/api/v2/players</c>. A readable player with no record in the mix is absent.
    /// </summary>
    /// <param name="mixValue">Required. A Phoenix mix from <c>/api/v2/mixes</c>; a legacy mix has no PUMBILITY and answers 404.</param>
    /// <param name="community">Only members of this community, as on <c>/api/v2/players</c>.</param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(CursorPageDto<PlayerStatsDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "community")] string? community = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var mixFailure))
            return mixFailure!;
        if (!mix.HasPumbility()) return NoPumbilityProblem(mix);

        var (ids, communityKey, failure) = await ReadableWithin(community);
        if (ids is null) return failure!;

        var fingerprint = ContinuationToken.FingerprintOf(CredentialKey(_currentUser), mix, pageSize, communityKey);
        ContinuationToken? from = null;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            from = token;
        }

        var all = ids.Count == 0
            ? Array.Empty<PlayerStatsRecord>()
            : (await _mediator.Send(new GetPlayersStatsQuery(ids, mix)))
            .OrderByDescending(s => s.SkillRating)
            .ThenBy(s => s.UserId)
            .ToArray();

        // A keyset on (PUMBILITY, player) rather than an offset, for the same reason the score
        // page uses one: the ranking moves while a tool crawls it, and an offset over a moving
        // list repeats or skips whoever crossed the page boundary. Both halves or neither, as on
        // the score page — a cursor with one segment blanked must not decode as "the start".
        if (from is not null && (from.Value.Key is null) != (from.Value.Id is null))
            return InvalidCursorProblem();

        var rows = all;
        if (from?.Key is not null && from.Value.Id is not null
                                  && double.TryParse(from.Value.Key, NumberStyles.Float, CultureInfo.InvariantCulture,
                                      out var afterRating))
            rows = all.Where(s => s.SkillRating < afterRating
                                  || (s.SkillRating == afterRating && s.UserId.CompareTo(from.Value.Id.Value) > 0))
                .ToArray();

        var page = rows.Take(pageSize).ToArray();
        var identities = await PlayerIdentities.Resolve(_mediator, page.Select(s => s.UserId).ToArray());
        var data = page
            .Where(s => identities.ContainsKey(s.UserId))
            .Select(s => new PlayerStatsDto(s.UserId, identities[s.UserId].Username, identities[s.UserId].GameTag, s))
            .ToArray();

        var next = rows.Length > page.Length
            ? ContinuationToken.FromKeyset(page[^1].SkillRating.ToString("R", CultureInfo.InvariantCulture),
                page[^1].UserId, fingerprint)
            : (ContinuationToken?)null;

        return Json(Page(data, pageSize, all.Length, next));
    }

    /// <summary>
    ///     One player's PUMBILITY numbers in one mix: the merged pool, the singles, doubles and
    ///     co-op pools, the competitive levels, the highest level passed, the clear count, and where
    ///     the site places them on piugame's official PUMBILITY ranking. What the site's PUMBILITY
    ///     page shows.
    /// </summary>
    /// <param name="playerId">A player id from <c>/api/v2/players</c>, or <c>me</c> with a personal token.</param>
    /// <param name="mixValue">Required. A Phoenix mix from <c>/api/v2/mixes</c>; a legacy mix has no PUMBILITY and answers 404.</param>
    [HttpGet("{playerId}/stats")]
    [ProducesResponseType(typeof(PlayerStatsDto), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlayerStats([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null)
    {
        var (resolved, failure) = await ResolvePlayer(playerId);
        if (resolved is null) return failure!;
        var userId = resolved.Value;
        if (!V2MixParser.TryParse(mixValue, out var mix)) return MixRequiredProblem();
        if (!mix.HasPumbility()) return NoPumbilityProblem(mix);

        // The bulk read rather than the single one: the single read answers a player with no row
        // with a zeroed record, and a zero PUMBILITY is not the same fact as no record at all.
        var stats = (await _mediator.Send(new GetPlayersStatsQuery(new[] { userId }, mix)))
            .FirstOrDefault(s => s.UserId == userId);
        if (stats is null) return NotFoundProblem("No record for that player in this mix.");

        var identities = await PlayerIdentities.Resolve(_mediator, new[] { userId });
        if (!identities.TryGetValue(userId, out var identity)) return Unreachable();

        return Json(new PlayerStatsDto(userId, identity.Username, identity.GameTag, stats));
    }

    /// <summary>The player's profile, with their most recently observed in-game tag.</summary>
    /// <param name="playerId">A player id from <c>/api/v2/players</c>, or <c>me</c> with a personal token.</param>
    [HttpGet("{playerId}")]
    [ProducesResponseType(typeof(PlayerV2Dto), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlayer([FromRoute] string playerId)
    {
        var (resolved, failure) = await ResolvePlayer(playerId);
        if (resolved is null) return failure!;
        var userId = resolved.Value;

        var user = await _mediator.Send(new GetUserByIdQuery(userId));
        if (user is null) return NotFoundProblem("No player with that id is readable with this credential.");

        return Json(new PlayerV2Dto(user, await ResolveGameTag(userId)));
    }

    /// <summary>
    ///     Best attempts in one mix.
    /// </summary>
    /// <param name="playerId">A player id from <c>/api/v2/players</c>, or <c>me</c> with a personal token.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="minLevel">Only charts at or above this level.</param>
    /// <param name="maxLevel">Only charts at or below this level.</param>
    /// <param name="chartTypeValue">Only charts of this type: Single, Double, CoOp, SinglePerformance, DoublePerformance.</param>
    /// <param name="isBroken">Only failed bests (true) or only passes (false).</param>
    /// <param name="recordedAfter">
    ///     Only records written after this instant. The incremental-sync parameter — with it a tool
    ///     stays current without webhooks and without re-reading a player's whole history.
    /// </param>
    /// <param name="chartIdsValue">
    ///     Only these charts: a comma-separated list of chart ids, at most 50. The point read for
    ///     "what did this player get on this chart", without paging their whole record.
    /// </param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet("{playerId}/scores")]
    [ProducesResponseType(typeof(PlayerScorePageDto), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScores([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "minLevel")] int? minLevel = null,
        [FromQuery(Name = "maxLevel")] int? maxLevel = null,
        [FromQuery(Name = "chartType")] string? chartTypeValue = null,
        [FromQuery(Name = "isBroken")] bool? isBroken = null,
        [FromQuery(Name = "recordedAfter")] DateTimeOffset? recordedAfter = null,
        [FromQuery(Name = "chartIds")] string? chartIdsValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        var (resolved, failure) = await ResolvePlayer(playerId);
        if (resolved is null) return failure!;
        var userId = resolved.Value;
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

        if (!TryParseChartIds(chartIdsValue, out var chartIds, out var chartIdsFailure)) return chartIdsFailure!;

        // The set rides the fingerprint in sorted form so the same charts in a different order
        // still validate the cursor they were issued under.
        var fingerprint = ContinuationToken.FingerprintOf(userId, mix, minLevel, maxLevel, chartType,
            isBroken, recordedAfter, pageSize,
            chartIds is null ? null : string.Join(",", chartIds.OrderBy(id => id)));
        ContinuationToken? from = null;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            from = token;
        }

        var charts = (await _mediator.Send(new GetChartsQuery(mix))).ToDictionary(c => c.Id);
        // Only a mix with a PUMBILITY formula can price a row — asking for one elsewhere throws.
        // Rows on any other mix report a null rating rather than a fabricated number.
        var scoring = mix.HasPumbility() ? ScoringConfiguration.PumbilityScoring(mix, true) : null;

        var rows = (await _mediator.Send(new GetPhoenixRecordsQuery(userId, mix)))
            .Where(r => charts.ContainsKey(r.ChartId))
            .Where(r => chartIds is null || chartIds.Contains(r.ChartId))
            .Where(r => recordedAfter is null || r.RecordedDate > recordedAfter.Value)
            .Where(r => isBroken is null || r.IsBroken == isBroken.Value)
            .Where(r => minLevel is null || (int)charts[r.ChartId].Level >= minLevel.Value)
            .Where(r => maxLevel is null || (int)charts[r.ChartId].Level <= maxLevel.Value)
            .Where(r => chartType is null || charts[r.ChartId].Type == chartType.Value)
            // Newest first, chart id as tiebreaker, matching the keyset the cursor carries.
            .OrderByDescending(r => r.RecordedDate)
            .ThenByDescending(r => r.ChartId)
            .ToArray();

        // Both halves or neither. TryDecode sets Id to null on an unparseable segment, and the
        // fingerprint is a plain hash inside the payload rather than a MAC — so a caller can take a
        // real cursor, blank the id segment and keep the fingerprint, and it decodes fine. That used
        // to dereference null and 500.
        if (from is not null && (from.Value.Key is null) != (from.Value.Id is null))
            return InvalidCursorProblem();

        if (from?.Key is not null && from.Value.Id is not null
                                  && DateTimeOffset.TryParse(from.Value.Key, out var afterDate))
            rows = rows.Where(r => r.RecordedDate < afterDate
                                   || (r.RecordedDate == afterDate && r.ChartId.CompareTo(from.Value.Id.Value) < 0))
                .ToArray();

        var page = rows.Take(pageSize).ToArray();
        var next = rows.Length > page.Length
            ? ContinuationToken.FromKeyset(page[^1].RecordedDate.ToString("O"), page[^1].ChartId, fingerprint)
            : (ContinuationToken?)null;

        return Json(new PlayerScorePageDto
        {
            Mix = mix.ToString(),
            ScoringModel = ScoringModelOf(mix),
            Limit = pageSize,
            Data = page.Select(r => new PlayerScoreDto(r, mix,
                scoring is null || r.Score is null
                    ? null
                    : Math.Round(scoring.GetScore(charts[r.ChartId], r.Score.Value,
                        r.Plate ?? PhoenixPlate.RoughGame, r.IsBroken), 2))).ToArray(),
            Next = next is null ? null : NextUrlFor(next.Value)
        });
    }

    /// <summary>
    ///     Play sessions, newest first: a player's imports in one mix until eight hours pass without
    ///     one, the grouping the site's Sessions page shows. Several imports in one night are one
    ///     session, listing every one of them in <c>sessionIds</c>.
    /// </summary>
    /// <param name="playerId">A player id from <c>/api/v2/players</c>, or <c>me</c> with a personal token.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet("{playerId}/sessions")]
    [ProducesResponseType(typeof(CursorPageDto<SessionDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessions([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        var (resolved, failure) = await ResolvePlayer(playerId);
        if (resolved is null) return failure!;
        var userId = resolved.Value;
        if (!TryReadRequest(mixValue, limit, out var mix, out var pageSize, out var mixFailure))
            return mixFailure!;

        var fingerprint = ContinuationToken.FingerprintOf(userId, mix, pageSize);
        var offset = 0;
        if (cursor is not null)
        {
            if (!ContinuationToken.TryDecode(cursor, fingerprint, out var token)) return InvalidCursorProblem();
            offset = token.Offset;
        }

        // The session read pages across every mix, so this walks it until the requested mix has
        // filled the window. It used to take one 500-group slice and filter that, which put a
        // silent ceiling on a heavy player: the tail vanished and the reported total was the count
        // within the slice rather than the real one. The walk that replaced it still asked for 500
        // and stepped by 500, while the read clamps a page to 50 — so it stopped after the newest
        // 50 sessions all the same. It steps by what the read actually returns now.
        //
        // Total is null rather than wrong. Counting a player's sessions in one mix means walking
        // every page to the end, which is the second full pass the envelope documents null for.
        const int readPage = GetRecentSessionsQuery.MaxPageSize;
        var wanted = offset + pageSize + 1;
        var filtered = new List<RecentSessionsPage.SessionGroup>();
        for (var page = 1; filtered.Count < wanted; page++)
        {
            var batch = await _mediator.Send(new GetRecentSessionsQuery(userId, page, readPage));
            if (batch.Groups.Count == 0) break;

            filtered.AddRange(batch.Groups.Where(g => g.Mix == mix));
            if (page * readPage >= batch.TotalGroups) break;
        }

        var rows = filtered.Skip(offset).Take(pageSize).Select(g => new SessionDto(g)).ToArray();
        var next = filtered.Count > offset + rows.Length
            ? ContinuationToken.FromOffset(offset + rows.Length, fingerprint)
            : (ContinuationToken?)null;

        return Json(Page(rows, pageSize, null, next));
    }

    /// <summary>
    ///     Every play, not just the ones that became records — the per-attempt history, with judgment
    ///     counts where the source carried them.
    /// </summary>
    /// <param name="playerId">A player id from <c>/api/v2/players</c>, or <c>me</c> with a personal token.</param>
    /// <param name="mixValue">Required. An enum name from <c>/api/v2/mixes</c>.</param>
    /// <param name="since">Only plays on or after this instant.</param>
    /// <param name="cursor">The opaque cursor from a previous page's <c>next</c> link.</param>
    /// <param name="limit">Rows per page, 1–500. Defaults to 100.</param>
    [HttpGet("{playerId}/journal")]
    [ProducesResponseType(typeof(CursorPageDto<JournalEntryDto>), StatusCodes.Status200OK, "application/json")]
    [ProducesProblem(StatusCodes.Status400BadRequest)]
    [ProducesProblem(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJournal([FromRoute] string playerId,
        [FromQuery(Name = "mix")] string? mixValue = null,
        [FromQuery(Name = "since")] DateTimeOffset? since = null,
        [FromQuery(Name = "cursor")] string? cursor = null,
        [FromQuery(Name = "limit")] int? limit = null)
    {
        var (resolved, failure) = await ResolvePlayer(playerId);
        if (resolved is null) return failure!;
        var userId = resolved.Value;
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
    /// <summary>
    ///     The player's game tag, newest mix first — a player on both Phoenix and Phoenix 2 has one
    ///     AM Pass tag, and the newer mix's row is the more recently confirmed snapshot of it.
    /// </summary>
    /// <remarks>
    ///     This used to return a "seen at" date alongside, which was null on every path and shipped
    ///     as a permanently-null wire field. Populating it honestly would mean a new OfficialMirror
    ///     contract query for a field nobody asked for, so the field went instead.
    /// </remarks>
    private async Task<string?> ResolveGameTag(Guid userId)
    {
        foreach (var mix in new[] { MixEnum.Phoenix2, MixEnum.Phoenix })
        {
            var tag = await _mediator.Send(new GetLinkedOfficialPlayerTagQuery(mix, userId));
            if (!string.IsNullOrWhiteSpace(tag)) return tag;
        }

        return null;
    }
}
