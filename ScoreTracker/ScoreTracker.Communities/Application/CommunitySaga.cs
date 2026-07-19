using MassTransit;
using MediatR;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Ucs.Contracts.Events;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;

namespace ScoreTracker.Communities.Application;

internal sealed class CommunitySaga : IRequestHandler<CreateCommunityCommand>, IRequestHandler<JoinCommunityCommand>,
    IRequestHandler<LeaveCommunityCommand>,
    IRequestHandler<GetCommunityMembersQuery, IEnumerable<Guid>>,
    IRequestHandler<GetCommunityLeaderboardQuery, IEnumerable<CommunityLeaderboardRecord>>,
    IRequestHandler<CreateInviteLinkCommand, Guid>,
    IRequestHandler<GetMyCommunitiesQuery, IEnumerable<CommunityOverviewRecord>>,
    IRequestHandler<GetPublicCommunitiesQuery, IEnumerable<CommunityOverviewRecord>>,
    IRequestHandler<GetCommunityCountQuery, int>,
    IRequestHandler<GetCommunityQuery, Community>,
    IRequestHandler<JoinCommunityByInviteCodeCommand>,
    IRequestHandler<GetCommunityInvitePreviewQuery, Contracts.CommunityInvitePreviewRecord?>,
    IRequestHandler<AddDiscordChannelToCommunityCommand>,
    IRequestHandler<RemoveDiscordChannelFromCommunityCommand>,
    IRequestHandler<GetPhoenixRecordsForCommunityQuery, IEnumerable<UserPhoenixScore>>,
    IConsumer<ScoreHighlightsCapturedEvent>,
    IConsumer<NewTitlesAcquiredEvent>,
    IConsumer<UserUpdatedEvent>,
    IConsumer<UcsLeaderboardPlacedEvent>

{
    private readonly IBotClient _bot;
    private readonly IChartRepository _charts;
    private readonly ICommunityRepository _communities;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly IScoreReader _scores;
    private readonly IUserReader _users;
    private readonly IPlayerStatsReader _playerStats;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly ILocalizedTextAccessor _localizer;

    public CommunitySaga(ICurrentUserAccessor currentUser, ICommunityRepository communities, IBotClient bot,
        IUserReader users, IChartRepository charts, IScoreReader scores, IMediator mediator,
        IPlayerStatsReader playerStats, IDateTimeOffsetAccessor dateTime, ILocalizedTextAccessor localizer)
    {
        _currentUser = currentUser;
        _communities = communities;
        _bot = bot;
        _users = users;
        _charts = charts;
        _scores = scores;
        _mediator = mediator;
        _playerStats = playerStats;
        _dateTime = dateTime;
        _localizer = localizer;
    }

    // Titles with no session — a zero-score import that still detected new badges, or an admin
    // recompute — get their own rich card in the session card's visual language (owner call:
    // ALL title completions show in a card). Chunked so a rare big badge dump never overruns
    // the char ceiling.
    public async Task Consume(ConsumeContext<NewTitlesAcquiredEvent> context)
    {
        var e = context.Message;
        var user = await _users.GetUser(e.UserId, context.CancellationToken);
        if (user == null) return;
        var titles = e.NewTitles.ToArray();
        await SendRichToCommunityDiscords(user.Id,
            culture => BuildTitlesCards(user, e.Mix, titles, e.ParagonUpgrades, culture),
            context.CancellationToken);
    }

    private IReadOnlyList<RichBotMessage> BuildTitlesCards(User user, MixEnum mix,
        IReadOnlyList<string> newTitles, IDictionary<string, string> paragonUpgrades, string? culture)
    {
        var lines = new List<string>();
        lines.AddRange(newTitles.OrderBy(t => t)
            .Select(t => "🏅 " + _localizer.Get(culture, "**{0}** completed", Bracket(t))));
        lines.AddRange(paragonUpgrades.OrderBy(p => p.Key)
            .Select(p => "🏅 " + _localizer.Get(culture, "**{0}** paragon → {1}",
                Bracket(p.Key), ParagonEmoji(p.Value))));
        if (lines.Count == 0) return Array.Empty<RichBotMessage>();

        var earned = TitlesEarnedText(newTitles.Count, paragonUpgrades.Count, culture);
        var links = user.IsPublic
            ? new[]
            {
                new RichBotLink(_localizer.Get(culture, "See more"),
                    new Uri($"{SiteBase}/Player/{user.Id}/Sessions"))
            }
            : Array.Empty<RichBotLink>();

        return lines.Chunk(TitleCardLineCap)
            .Select(chunk => new RichBotMessage(
                new RichBotSection($"### {MixPrefix(mix)}**{user.Name}** — {earned}", user.ProfileImage),
                new IRichBotBlock[] { new RichBotDivider(), new RichBotText(string.Join("\n", chunk)) },
                $"#MIX|{mix}# {mix.GetName()} · PIU Scores",
                mix.GetAccentColor(), links))
            .ToArray();
    }

    private string TitlesEarnedText(int titles, int paragons, string? culture)
    {
        var parts = new List<string>();
        if (titles > 0)
            parts.Add(titles == 1
                ? _localizer.Get(culture, "1 title")
                : _localizer.Get(culture, "{0} titles", titles));
        if (paragons > 0)
            parts.Add(paragons == 1
                ? _localizer.Get(culture, "1 paragon")
                : _localizer.Get(culture, "{0} paragons", paragons));
        return string.Join(" · ", parts);
    }

    // The session snapshot (design doc revision 2): ONE card per score batch — stats
    // that moved, achievements earned, and only the scores worth reading; everything
    // else is a count. Renders from ScoreHighlightsCapturedEvent, which the capture
    // orchestrator publishes AFTER the rating/title steps ran, so every section is
    // deterministic. This is the only score-triggered community Discord message; the old
    // ratings/weekly messages are retired. Site-detected title completions ride this card
    // too when their import saved scores (iteration 3); a zero-score import's titles get
    // their own rich card via NewTitlesAcquiredEvent instead.
    private const int ArtRowCap = 5;
    private const int NotableRowCap = 10;
    private const int MoreScoresCap = 10;
    private const int CoOpScoresCap = 5;
    private const int WeeklyLineCap = 4;
    private const int ProgressDeltaCap = 3;
    private const int FolderLineCap = 6;
    private const int TitleCardLineCap = 20;
    private const int BigGainThreshold = 10000;
    private const string SiteBase = "https://piuscores.arroweclip.se";

    // Discord's Components V2 text ceiling is 4000 chars measured AFTER emoji tokens expand
    // (#DIFFICULTY|…# → <:piu_…:snowflake>). The saga can't see the real snowflakes, so it
    // packs against a conservative estimate: literal length + a per-token width, kept under a
    // margin so nothing reaches the renderer's mid-line truncation. The stats, achievements
    // (all titles), and folder line are reserved first; only the score buckets flex to fit.
    private const int TokenWidth = 30;
    private const int CardCharBudget = 3600;
    private const int FooterReserve = 90;
    private static readonly string[] EmojiTokenPrefixes =
        { "#DIFFICULTY|", "#LETTERGRADE|", "#PLATE|", "#MIX|" };

    public async Task Consume(ConsumeContext<ScoreHighlightsCapturedEvent> context)
    {
        var e = context.Message;
        var user = await _users.GetUser(e.UserId, context.CancellationToken);
        if (user == null) return;

        var bests = (await _scores.GetBestScores(e.Mix, e.UserId, context.CancellationToken))
            .Where(s => s.Score != null)
            .ToDictionary(s => s.ChartId);
        var charts = (await _charts.GetCharts(e.Mix,
                chartIds: e.Changes.Select(c => c.ChartId).Distinct(),
                cancellationToken: context.CancellationToken))
            .ToDictionary(c => c.Id);
        var scoringLevels = await _mediator.Send(new GetChartScoringLevelsQuery(e.Mix), context.CancellationToken);

        var known = e.Changes
            .Where(c => charts.ContainsKey(c.ChartId) && bests.ContainsKey(c.ChartId))
            .ToArray();
        if (known.Length == 0) return;

        // The 💥 row: the session's single biggest upscore, when it cleared the
        // threshold (owner call: +10k). It earns a row even with no other flag.
        var bigGain = known
            .Where(c => !c.IsBroken && c.OldScore != null && c.NewScore != null &&
                        c.NewScore.Value - c.OldScore.Value >= BigGainThreshold)
            .OrderByDescending(c => c.NewScore!.Value - c.OldScore!.Value)
            .FirstOrDefault();

        bool Notable(ScoreHighlightsCapturedEvent.HighlightedChange c)
        {
            return c.Flags != HighlightFlags.None || ReferenceEquals(c, bigGain);
        }

        // Non-co-op rows in the universal noteworthy order (difficulty desc, scoring level
        // desc, score desc): flagged rows lead as art/text rows and own the captions, the
        // rest fall to the compact "More scores" block, anything past that to the grouped
        // overflow line.
        var standard = known
            .Where(c => charts[c.ChartId].Type != ChartType.CoOp)
            .OrderByDescending(Notable)
            .ThenByDescending(c => (int)charts[c.ChartId].Level)
            .ThenByDescending(c => scoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
            .ThenByDescending(c => (int)(bests[c.ChartId].Score ?? 0))
            .ToArray();
        var notable = standard.Where(Notable).Take(NotableRowCap).ToArray();
        var notableIds = notable.Select(c => c.ChartId).ToHashSet();
        // Re-sort the remainder purely by level (owner call): when notable overflows its cap
        // the extra flagged rows are lower-level, and leaving them in the notable-first order
        // would float them above higher-level unflagged charts. "More scores" is one clean run.
        var moreScores = standard.Where(c => !notableIds.Contains(c.ChartId))
            .OrderByDescending(c => (int)charts[c.ChartId].Level)
            .ThenByDescending(c => scoringLevels.TryGetValue(c.ChartId, out var sl) ? sl : 0)
            .ThenByDescending(c => (int)(bests[c.ChartId].Score ?? 0))
            .Take(MoreScoresCap)
            .ToArray();

        // Co-ops get their own compact bucket (owner call): up to 5, community co-op pass
        // difficulty descending. They no longer take art rows.
        var coOpChanges = known.Where(c => charts[c.ChartId].Type == ChartType.CoOp).ToArray();
        var coOpScores = Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>();
        if (coOpChanges.Length > 0)
        {
            var coOpRatings = (await _mediator.Send(new GetCoOpRatingsQuery(), context.CancellationToken))
                .ToDictionary(r => r.ChartId, r => r.Ratings.Count > 0 ? r.Ratings.Values.Max(l => (int)l) : 0);
            coOpScores = coOpChanges
                .OrderByDescending(c => coOpRatings.GetValueOrDefault(c.ChartId, 0))
                .ThenByDescending(c => (int)(bests[c.ChartId].Score ?? 0))
                .Take(CoOpScoresCap)
                .ToArray();
        }

        // The weekly read: current placements for whichever batch charts sit on this
        // week's board. Gated on competitive − 5 (owner call) exactly like the peer flag —
        // a 23-competitive player placing on a weekly D10 is the same noise. Failure costs
        // the weekly lines, never the card.
        var weekly = Array.Empty<WeeklyPlacementRecord>();
        try
        {
            var stats = await _playerStats.GetStats(e.Mix, e.UserId, context.CancellationToken);
            weekly = (await _mediator.Send(new GetUserWeeklyPlacementsQuery(e.UserId, e.Mix,
                    known.Select(c => c.ChartId).Distinct().ToArray()), context.CancellationToken))
                .Where(w => (int)charts[w.ChartId].Level >= CompetitiveFor(charts[w.ChartId].Type, stats) - 5)
                .OrderByDescending(w => (int)charts[w.ChartId].Level)
                .Take(WeeklyLineCap)
                .ToArray();
        }
        catch
        {
            // The board read is a flex, not a fact the card owes anyone.
        }

        // The Daily Step read: the player's standing on today's shared chart, but only when this
        // batch actually includes it (they played it this session) — the same "freshly imported"
        // gate as the weekly lines. Best-effort; a failure costs the line, never the card.
        DailyStepPlacement? daily = null;
        var dailyChartId = Guid.Empty;
        try
        {
            var board = await _mediator.Send(new GetDailyStepQuery(e.Mix), context.CancellationToken);
            if (board != null && known.Any(c => c.ChartId == board.ChartId))
            {
                dailyChartId = board.ChartId;
                daily = await _mediator.Send(new GetDailyStepPlacementQuery(e.UserId, e.Mix),
                    context.CancellationToken);
            }
        }
        catch
        {
            // ditto — the daily standing is a flex.
        }

        var reclears = await CrossMixReclears(e, known, context.CancellationToken);

        // The folder breakdown's reads happen once here — its line is language-neutral, so
        // the per-culture renders below share it.
        var passCharts = known
            .Where(c => c.IsNewPass && !c.IsBroken)
            .Select(c => charts[c.ChartId])
            .Where(c => c.Type is ChartType.Single or ChartType.Double);
        var folderStats = await FolderProgress(e.Mix, e.UserId, passCharts, FolderLineCap,
            context.CancellationToken);

        var inputs = new SnapshotInputs(e, user, known, notable, moreScores, coOpScores, charts, bests, weekly,
            daily, dailyChartId, reclears);
        await SendRichToCommunityDiscords(user.Id,
            culture => new[] { BuildSnapshotCard(inputs, folderStats, culture) }, context.CancellationToken);
    }

    // A new pass on a chart the player already cleared (non-broken) in another mix is a
    // cross-mix reclear — the same canonical chart id reappears in that mix's best scores.
    // Only new passes qualify; upscores and broken plays never do. Reads the other
    // Phoenix-family mix plus legacy XX, matching the tier list's "passed in another mix"
    // mark. Returns just the batch chart ids that are reclears, so an upscore-only or
    // first-clear batch skips the cross-mix reads entirely.
    private async Task<IReadOnlySet<Guid>> CrossMixReclears(ScoreHighlightsCapturedEvent e,
        IReadOnlyList<ScoreHighlightsCapturedEvent.HighlightedChange> known, CancellationToken cancellationToken)
    {
        var candidates = known.Where(c => c.IsNewPass && !c.IsBroken).Select(c => c.ChartId).ToHashSet();
        if (candidates.Count == 0) return candidates;

        var clearedElsewhere = new HashSet<Guid>();
        foreach (var otherMix in Enum.GetValues<MixEnum>())
        {
            if (otherMix == e.Mix || otherMix.UsesLegacyScoring()) continue;
            foreach (var score in await _scores.GetBestScores(otherMix, e.UserId, cancellationToken))
                if (!score.IsBroken) clearedElsewhere.Add(score.ChartId);
        }

        if (e.Mix != MixEnum.XX)
            foreach (var attempt in await _scores.GetBestXXAttempts(e.UserId, cancellationToken))
                if (attempt.BestAttempt is { IsBroken: false }) clearedElsewhere.Add(attempt.Chart.Id);

        candidates.IntersectWith(clearedElsewhere);
        return candidates;
    }

    /// <summary>Everything the snapshot card renders from, shaped by the consumer.</summary>
    private sealed record SnapshotInputs(
        ScoreHighlightsCapturedEvent E,
        User User,
        ScoreHighlightsCapturedEvent.HighlightedChange[] Known,
        ScoreHighlightsCapturedEvent.HighlightedChange[] Notable,
        ScoreHighlightsCapturedEvent.HighlightedChange[] MoreScores,
        ScoreHighlightsCapturedEvent.HighlightedChange[] CoOpScores,
        Dictionary<Guid, Chart> Charts,
        Dictionary<Guid, RecordedPhoenixScore> Bests,
        WeeklyPlacementRecord[] Weekly,
        DailyStepPlacement? Daily,
        Guid DailyChartId,
        IReadOnlySet<Guid> Reclears);

    private RichBotMessage BuildSnapshotCard(SnapshotInputs inputs, string folderStats, string? culture)
    {
        var header = HeaderSection(inputs, culture);
        var statLines = StatLines(inputs.E.Milestones, culture);
        var achievementLines = AchievementLines(inputs.E, inputs.Weekly, inputs.Charts, inputs.Daily,
            inputs.DailyChartId, culture);

        // The folder breakdown was computed by the consumer and is reserved up front (owner
        // call): scores yield to it, never the reverse, so a title-heavy or score-heavy card
        // still ends with it.
        var remaining = CardCharBudget - FooterReserve
            - Estimate(header.Markdown)
            - EstimateLines(statLines)
            - EstimateLines(achievementLines)
            - (string.IsNullOrWhiteSpace(folderStats) ? 0 : Estimate(folderStats));

        var blocks = new List<IRichBotBlock> { new RichBotDivider() };

        // ① Stats that moved (capture already floored the noise).
        AddSection(blocks, statLines);
        // ② Achievements: titles, paragon, folder lamps, weekly placements — or the
        // per-title progress deltas when nothing completed.
        AddSection(blocks, achievementLines);
        // ③ Notable scores lead, then the compact buckets — each filling only what the
        // reserved sections leave. Whatever doesn't fit (or overruns a cap) is a count line.
        var shown = new HashSet<Guid>();
        AddNotableRows(blocks, inputs, culture, ref remaining, shown);
        AddCompactBucket(blocks, inputs.MoreScores, inputs, _localizer.Get(culture, "More scores"),
            ref remaining, shown);
        AddCompactBucket(blocks, inputs.CoOpScores, inputs, _localizer.Get(culture, "Co-op"),
            ref remaining, shown);
        AddOverflowLine(blocks, inputs, culture, shown);

        if (!string.IsNullOrWhiteSpace(folderStats))
        {
            blocks.Add(new RichBotDivider());
            blocks.Add(new RichBotText(folderStats));
        }

        // Footnote the reclear asterisk only when a marked row actually rendered — reclears
        // that fell to the compressed overflow count carry no visible mark to explain.
        var reclearNote = shown.Overlaps(inputs.Reclears)
            ? " · " + _localizer.Get(culture, "\\* = reclears")
            : string.Empty;
        return new RichBotMessage(header, blocks,
            $"#MIX|{inputs.E.Mix}# {inputs.E.Mix.GetName()} · PIU Scores{reclearNote}",
            inputs.E.Mix.GetAccentColor(), Links(inputs, culture));
    }

    // Estimated rendered width of a block once emoji tokens expand — literal length plus a
    // conservative per-token width. Over-estimating is safe: it trims early, never late.
    private static int Estimate(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return 0;
        var tokens = 0;
        foreach (var prefix in EmojiTokenPrefixes)
        {
            var idx = markdown.IndexOf(prefix, StringComparison.Ordinal);
            while (idx >= 0)
            {
                tokens++;
                idx = markdown.IndexOf(prefix, idx + prefix.Length, StringComparison.Ordinal);
            }
        }

        return markdown.Length + tokens * TokenWidth;
    }

    private static int EstimateLines(List<string> lines)
    {
        return lines.Count == 0 ? 0 : Estimate(string.Join("\n", lines));
    }

    private static void AddSection(List<IRichBotBlock> blocks, List<string> lines)
    {
        if (lines.Count == 0) return;
        blocks.Add(new RichBotText(string.Join("\n", lines)));
        blocks.Add(new RichBotDivider());
    }

    private void AddNotableRows(List<IRichBotBlock> blocks, SnapshotInputs inputs, string? culture,
        ref int remaining, HashSet<Guid> shown)
    {
        var artLeft = ArtRowCap;
        foreach (var change in inputs.Notable)
        {
            var chart = inputs.Charts[change.ChartId];
            var text = RowText(change, chart, inputs.Bests[change.ChartId], IsBigGain(change, inputs.Known),
                ReclearMark(inputs, change.ChartId), culture);
            var cost = Estimate(text);
            // Notable rows are the priciest; when one won't fit, the rest fall to overflow
            // (the cheaper compact buckets still get their turn at the leftover budget).
            if (cost > remaining) break;
            remaining -= cost;
            shown.Add(change.ChartId);
            blocks.Add(artLeft-- > 0 ? new RichBotSection(text, chart.Song.ImagePath) : new RichBotText(text));
        }
    }

    // The low-ceremony buckets (owner call): one compact line per score in a single text
    // block — difficulty, song, score, grade — no art, no caption. Non-co-op "More scores"
    // and co-op each get their own labelled block, filling as many rows as the budget allows.
    private static void AddCompactBucket(List<IRichBotBlock> blocks,
        ScoreHighlightsCapturedEvent.HighlightedChange[] rows, SnapshotInputs inputs, string label,
        ref int remaining, HashSet<Guid> shown)
    {
        if (rows.Length == 0) return;
        var used = Estimate($"-# {label}");
        var lines = new List<string>();
        foreach (var c in rows)
        {
            var row = CompactRow(inputs.Charts[c.ChartId], inputs.Bests[c.ChartId], ReclearMark(inputs, c.ChartId));
            var cost = Estimate(row) + 1; // + newline
            if (used + cost > remaining) break;
            used += cost;
            lines.Add(row);
            shown.Add(c.ChartId);
        }

        if (lines.Count == 0) return;
        remaining -= used;
        blocks.Add(new RichBotText($"-# {label}\n" + string.Join("\n", lines)));
    }

    private static string CompactRow(Chart chart, RecordedPhoenixScore best, string reclearMark)
    {
        return $"#DIFFICULTY|{chart.DifficultyString}# {chart.Song.Name}{reclearMark} — **{(int)best.Score!.Value:N0}** " +
               $"#LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#";
    }

    private void AddOverflowLine(List<IRichBotBlock> blocks, SnapshotInputs inputs, string? culture,
        HashSet<Guid> shown)
    {
        // `shown` is what actually rendered (budget-trimmed), so the count reflects the real
        // remainder — not just what the row caps would have dropped.
        var rest = inputs.Known.Where(c => !shown.Contains(c.ChartId)).ToArray();
        if (rest.Length == 0) return;

        var parts = rest
            .Where(c => inputs.Charts[c.ChartId].Type != ChartType.CoOp)
            .GroupBy(c => inputs.Charts[c.ChartId].DifficultyString)
            .OrderByDescending(g => (int)inputs.Charts[g.First().ChartId].Level)
            .Select(g => g.Count() == 1 ? g.Key : $"{g.Key} ×{g.Count()}")
            .ToList();
        var restCoOps = rest.Count(c => inputs.Charts[c.ChartId].Type == ChartType.CoOp);
        if (restCoOps > 0) parts.Add($"CO-OP ×{restCoOps}");
        blocks.Add(new RichBotText(_localizer.Get(culture, "+{0} more: {1}", rest.Length,
            string.Join(", ", parts))));
    }

    private RichBotSection HeaderSection(SnapshotInputs inputs, string? culture)
    {
        var span = LevelSpan(inputs.Known
            .Where(c => inputs.Charts[c.ChartId].Type != ChartType.CoOp)
            .Select(c => inputs.Charts[c.ChartId]).ToArray());
        if (inputs.Known.Any(c => inputs.Charts[c.ChartId].Type == ChartType.CoOp))
            span = span.Length > 0 ? $"{span} · CO-OP" : "CO-OP";
        var header =
            $"### {MixPrefix(inputs.E.Mix)}**{inputs.User.Name}** — {CountsText(inputs.Known, culture)}" +
            (span.Length > 0 ? $"\n-# {span}" : string.Empty);
        return new RichBotSection(header, inputs.User.ProfileImage);
    }

    private string CountsText(ScoreHighlightsCapturedEvent.HighlightedChange[] known, string? culture)
    {
        var passes = known.Count(c => c.IsNewPass && !c.IsBroken);
        var upscores = known.Count(c => !c.IsNewPass && !c.IsBroken);
        if (passes > 0 && upscores > 0)
            return _localizer.Get(culture, "passed {0:N0} · upscored {1:N0}", passes, upscores);
        if (passes > 0)
            return passes == 1
                ? _localizer.Get(culture, "passed 1 chart")
                : _localizer.Get(culture, "passed {0:N0} charts", passes);
        if (upscores > 0)
            return upscores == 1
                ? _localizer.Get(culture, "upscored 1 chart")
                : _localizer.Get(culture, "upscored {0:N0} charts", upscores);
        return known.Length == 1
            ? _localizer.Get(culture, "updated 1 chart")
            : _localizer.Get(culture, "updated {0:N0} charts", known.Length);
    }

    // The deep link only renders for public players — the Sessions page redirects
    // everyone else home anyway.
    private RichBotLink[] Links(SnapshotInputs inputs, string? culture)
    {
        if (!inputs.User.IsPublic) return Array.Empty<RichBotLink>();
        var session = inputs.E.SessionId == null ? string.Empty : $"?session={inputs.E.SessionId}";
        return new[]
        {
            new RichBotLink(_localizer.Get(culture, "See more"),
                new Uri($"{SiteBase}/Player/{inputs.User.Id}/Sessions{session}"))
        };
    }

    /// <summary>The 💥 row: the session's single biggest upscore, when ≥ the threshold.</summary>
    private static bool IsBigGain(ScoreHighlightsCapturedEvent.HighlightedChange change,
        ScoreHighlightsCapturedEvent.HighlightedChange[] known)
    {
        if (change.IsBroken || change.OldScore == null || change.NewScore == null) return false;
        var gain = change.NewScore.Value - change.OldScore.Value;
        if (gain < BigGainThreshold) return false;
        return gain == known
            .Where(c => !c.IsBroken && c.OldScore != null && c.NewScore != null)
            .Max(c => c.NewScore!.Value - c.OldScore!.Value);
    }

    // A cross-mix reclear gets a trailing asterisk on its row, escaped so Discord renders it
    // literally rather than reading it as emphasis against an adjacent bold song link. The set
    // holds only new-pass reclears, so membership alone qualifies the mark.
    private static string ReclearMark(SnapshotInputs inputs, Guid chartId)
    {
        return inputs.Reclears.Contains(chartId) ? "\\*" : string.Empty;
    }

    private string RowText(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best, bool bigGain, string reclearMark, string? culture)
    {
        return change.IsNewPass
            ? PassRow(change, chart, best, bigGain, reclearMark, culture)
            : UpscoreRow(change, chart, best, bigGain, culture);
    }

    private List<string> StatLines(IReadOnlyList<PlayerMilestoneRecord> milestones, string? culture)
    {
        var lines = new List<string>();
        foreach (var m in milestones)
            switch (m.Kind)
            {
                case MilestoneKind.PumbilityGain:
                    lines.Add("📈 " + _localizer.Get(culture, "**PUMBILITY** {0:N0} → **{1:N0}** (+{2:N0})",
                        m.OldValue, m.NewValue, m.NewValue - m.OldValue));
                    break;
                case MilestoneKind.SinglesPumbilityGain:
                    lines.Add("📈 " + _localizer.Get(culture, "**PUMBILITY (S)** {0:N0} → **{1:N0}** (+{2:N0})",
                        m.OldValue, m.NewValue, m.NewValue - m.OldValue));
                    break;
                case MilestoneKind.DoublesPumbilityGain:
                    lines.Add("📈 " + _localizer.Get(culture, "**PUMBILITY (D)** {0:N0} → **{1:N0}** (+{2:N0})",
                        m.OldValue, m.NewValue, m.NewValue - m.OldValue));
                    break;
                case MilestoneKind.SinglesCompetitiveGain:
                    lines.Add("📈 " + _localizer.Get(culture, "**Singles competitive** {0:0.00} → **{1:0.00}**",
                        m.OldValue, m.NewValue));
                    break;
                case MilestoneKind.DoublesCompetitiveGain:
                    lines.Add("📈 " + _localizer.Get(culture, "**Doubles competitive** {0:0.00} → **{1:0.00}**",
                        m.OldValue, m.NewValue));
                    break;
            }

        return lines;
    }

    private List<string> AchievementLines(ScoreHighlightsCapturedEvent e,
        WeeklyPlacementRecord[] weekly, Dictionary<Guid, Chart> charts,
        DailyStepPlacement? daily, Guid dailyChartId, string? culture)
    {
        var lines = new List<string>();
        var titles = e.Milestones.Where(m => m.Kind == MilestoneKind.TitleCompleted).ToArray();
        // Every completion is listed — titles are the card's top priority (owner call), and
        // the 4000-char budget (not a name cap) is the only backstop.
        lines.AddRange(titles.Select(t =>
            "🏅 " + _localizer.Get(culture, "**{0}** completed", Bracket(t.Title))));

        // Paragon gains are never counted or aggregated — the new grade IS the content
        // (owner call), so every gain is its own grade-named line.
        var paragons = e.Milestones.Where(m => m.Kind == MilestoneKind.ParagonLevelGain).ToArray();
        lines.AddRange(paragons.Select(p => "🏅 " + _localizer.Get(culture, "**{0}** paragon → {1}",
            Bracket(p.Title), ParagonEmoji(p.Detail))));

        foreach (var lamp in e.Milestones.Where(m => m.Kind is MilestoneKind.FolderPassLamp
                     or MilestoneKind.FolderGradeLamp or MilestoneKind.FolderPlateLamp))
            lines.Add(LampLine(lamp, culture));

        lines.AddRange(weekly.Select(w =>
            "🏆 " + _localizer.Get(culture, "**#{0}** on {1} {2} weekly",
                w.Place, charts[w.ChartId].Song.Name,
                $"#DIFFICULTY|{charts[w.ChartId].DifficultyString}#")));

        // Daily Step standing (no competitive gate — the shared daily is a communal event, and a
        // Limbo-day placement on an easy chart is the whole point).
        if (daily != null && charts.ContainsKey(dailyChartId))
            lines.Add("🏆 " + _localizer.Get(culture,
                daily.IsLimbo ? "**#{0}** on {1} {2} Daily Step (Limbo)" : "**#{0}** on {1} {2} Daily Step",
                daily.Place, charts[dailyChartId].Song.Name,
                $"#DIFFICULTY|{charts[dailyChartId].DifficultyString}#"));

        // Generic title progress (difficulty/co-op) always rides the top section, nearest to
        // complete first (owner call) — completed titles show only their completion line, and
        // chart-specific skill progress rides the per-row caption instead.
        lines.AddRange(e.TitleProgress.Take(ProgressDeltaCap).Select(d =>
            "🏅 " + _localizer.Get(culture, "{0} {1}% → **{2}%**",
                Bracket(d.Title), (int)(d.OldPercent * 100), (int)(d.NewPercent * 100))));

        return lines;
    }

    private static string ParagonEmoji(string? detail)
    {
        return detail switch
        {
            null or "" or "None" => detail ?? string.Empty,
            "PG" => "#PLATE|PerfectGame#",
            _ => $"#LETTERGRADE|{detail}|False#"
        };
    }

    private string LampLine(PlayerMilestoneRecord m, string? culture)
    {
        var detail = (m.Detail ?? string.Empty).Split('|');
        return m.Kind switch
        {
            MilestoneKind.FolderPassLamp =>
                $"🎉 #DIFFICULTY|{detail[0]}# {_localizer.Get(culture, "**All passed!**")}",
            MilestoneKind.FolderGradeLamp when detail.Length == 2 =>
                $"🏆 #DIFFICULTY|{detail[0]}# {_localizer.Get(culture, "**All {0} or better**", detail[1])}",
            MilestoneKind.FolderPlateLamp when detail.Length == 2 =>
                $"🏆 #DIFFICULTY|{detail[0]}# {_localizer.Get(culture, "**All {0} or better**", $"#PLATE|{detail[1]}#")}",
            _ => $"🏆 {m.Detail}"
        };
    }

    private async Task<string> FolderProgress(MixEnum mix, Guid userId, IEnumerable<Chart> passCharts,
        int? topFolders, CancellationToken cancellationToken)
    {
        var groups = passCharts.GroupBy(c => (c.Type, c.Level))
            .OrderByDescending(g => g.Count()).ThenByDescending(g => (int)g.Key.Level)
            .ToArray();
        if (topFolders != null) groups = groups.Take(topFolders.Value).ToArray();
        var parts = new List<string>();
        foreach (var group in groups.OrderByDescending(g => (int)g.Key.Level).ThenBy(g => g.Key.Type))
        {
            var total = (await _mediator.Send(new GetChartsQuery(mix, group.Key.Level, group.Key.Type),
                cancellationToken)).Count();
            var clears = await _scores.GetClearCount(mix, userId, group.Key.Type, group.Key.Level,
                cancellationToken);
            if (total > 0)
                parts.Add(
                    $"#DIFFICULTY|{group.Key.Type.GetShortHand()}{group.Key.Level}# {clears}/{total}");
        }

        return string.Join(" · ", parts);
    }

    private static string LevelSpan(IReadOnlyList<Chart> charts)
    {
        var parts = new List<string>();
        foreach (var (type, shortHand) in new[] { (ChartType.Single, "S"), (ChartType.Double, "D") })
        {
            var levels = charts.Where(c => c.Type == type).Select(c => (int)c.Level).ToArray();
            if (!levels.Any()) continue;
            var min = levels.Min();
            var max = levels.Max();
            parts.Add(min == max ? $"{shortHand}{min}" : $"{shortHand}{min}–{shortHand}{max}");
        }

        return string.Join(" · ", parts);
    }

    private string PassRow(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best, bool bigGain, string reclearMark, string? culture)
    {
        return $"#DIFFICULTY|{chart.DifficultyString}# {SongLink(change, chart, bigGain)}{reclearMark}\n" +
               $"**{(int)best.Score!.Value:N0}** #LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#" +
               FlagCaption(change, chart, best, bigGain, culture);
    }

    private string UpscoreRow(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best, bool bigGain, string? culture)
    {
        var row = $"#DIFFICULTY|{chart.DifficultyString}# {SongLink(change, chart, bigGain)} " +
                  $"**{(int)best.Score!.Value:N0}**";
        if (change.OldScore != null)
        {
            row += $" (+{(int)best.Score!.Value - change.OldScore.Value:N0})";
            var oldLetter = PhoenixScore.From(change.OldScore.Value).LetterGrade;
            if (oldLetter != best.Score!.Value.LetterGrade)
                row += $" #LETTERGRADE|{oldLetter}|False# →";
        }

        return row + $" #LETTERGRADE|{best.Score!.Value.LetterGrade}|{best.IsBroken}##PLATE|{best.Plate}#" +
               FlagCaption(change, chart, best, bigGain, culture);
    }

    private static string SongLink(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        bool bigGain)
    {
        var link = $"[{chart.Song.Name}]({SiteBase}/Chart/{chart.Id})";
        return change.Flags == HighlightFlags.None && !bigGain ? link : $"**{link}**";
    }

    // The why-it's-noteworthy caption, rendered as Discord subtext under the score. Each flag
    // renders its captured detail — the pumbility rank, the peer standing (or PG ratio), the
    // skill title's score/threshold, the folder-debut ordinal. Vocabulary mirrors the
    // Sessions page badges.
    private string FlagCaption(ScoreHighlightsCapturedEvent.HighlightedChange change, Chart chart,
        RecordedPhoenixScore best, bool bigGain, string? culture)
    {
        var flags = change.Flags;
        if (flags == HighlightFlags.None && !bigGain) return string.Empty;
        var d = change.Detail;
        var parts = new List<string>();
        if (flags.HasFlag(HighlightFlags.PumbilityTop50))
            parts.Add("👑 " + (d?.PumbilityRank != null
                ? _localizer.Get(culture, "#{0} in your PUMBILITY", d.PumbilityRank)
                : _localizer.Get(culture, "PUMBILITY top 50")));
        if (flags.HasFlag(HighlightFlags.ScoreQuality90)) parts.Add(PeerCaption(d, best, culture));
        if (flags.HasFlag(HighlightFlags.TitleProgress)) parts.Add(SkillCaption(d, culture));
        if (flags.HasFlag(HighlightFlags.FolderDebut))
            parts.Add("🆕 " + (d?.FolderDebutOrdinal != null
                ? $"{Ordinal(d.FolderDebutOrdinal.Value, culture)} {chart.Type.GetShortHand()}{(int)chart.Level}"
                : _localizer.Get(culture, "Folder debut")));
        if (flags.HasFlag(HighlightFlags.FolderCompletion90))
            parts.Add("📁 " + _localizer.Get(culture, "Nearly complete folder"));
        if (flags.HasFlag(HighlightFlags.CompetitiveImprover))
            parts.Add("⬆ " + _localizer.Get(culture, "Raised competitive level"));
        if (bigGain) parts.Add("💥 " + _localizer.Get(culture, "Biggest gain of the session"));
        return "\n-# " + string.Join(" · ", parts);
    }

    private string PeerCaption(HighlightDetail? d, RecordedPhoenixScore best, string? culture)
    {
        if (d?.PeerCount is null or 0) return "📊 " + _localizer.Get(culture, "Top scores among peers");
        var isPg = best.Score != null && (int)best.Score.Value == 1_000_000;
        if (isPg && d.PeerPgCount != null)
            return "📊 " + _localizer.Get(culture, "PG · {0} of {1} peers have it", d.PeerPgCount, d.PeerCount);
        return "📊 " + _localizer.Get(culture, "#{0} of {1} peers", (d.PeerBetterCount ?? 0) + 1, d.PeerCount);
    }

    private string SkillCaption(HighlightDetail? d, string? culture)
    {
        if (d?.SkillTitleName == null) return "🏅 " + _localizer.Get(culture, "Title progress");
        return d.SkillTitleScore != null && d.SkillTitleThreshold != null
            ? $"🏅 {Bracket(d.SkillTitleName)} ({Abbrev(d.SkillTitleScore.Value)}/{Abbrev(d.SkillTitleThreshold.Value)})"
            : $"🏅 {Bracket(d.SkillTitleName)}";
    }

    // In-game titles show bracketed ([Expert Lv. 4]); skill/co-op/boss names already carry
    // their own bracket, so wrap only when one isn't there.
    private static string Bracket(string? title)
    {
        if (string.IsNullOrEmpty(title)) return string.Empty;
        return title.StartsWith('[') ? title : $"[{title}]";
    }

    private string Ordinal(int n, string? culture)
    {
        return n switch
        {
            1 => _localizer.Get(culture, "First"),
            2 => _localizer.Get(culture, "Second"),
            3 => _localizer.Get(culture, "Third"),
            _ => $"#{n}"
        };
    }

    // Floored to thousands so a near-threshold score never rounds up to a false complete.
    private static string Abbrev(int score)
    {
        return $"{score / 1000}k";
    }

    // The competitive level for a chart's type; co-op (and anything without a competitive
    // side) returns 0, so its gate threshold is −5 and it never gets filtered.
    private static double CompetitiveFor(ChartType type, PlayerStatsRecord stats)
    {
        return type switch
        {
            ChartType.Single => stats.SinglesCompetitiveLevel,
            ChartType.Double => stats.DoublesCompetitiveLevel,
            _ => 0
        };
    }

    public async Task Consume(ConsumeContext<UserUpdatedEvent> context)
    {
        if (context.Message.IsPublic)
            await JoinSystemCommunity("World", context.Message.UserId, context.CancellationToken);
        else if (await _communities.GetCommunityByName("World", context.CancellationToken) != null)
            await _mediator.Send(new LeaveCommunityCommand("World", context.Message.UserId));

        if (context.Message.Country != null)
            await JoinSystemCommunity(context.Message.Country, context.Message.UserId, context.CancellationToken);
    }

    // System communities (World + one per country) have no seed anywhere — a fresh
    // database throws CommunityNotFoundException on the first profile update. They
    // create themselves on first join instead: public, regional, unowned.
    private async Task JoinSystemCommunity(Name name, Guid userId, CancellationToken cancellationToken)
    {
        if (await _communities.GetCommunityByName(name, cancellationToken) == null)
            await _communities.SaveCommunity(new Community(name, Guid.Empty, CommunityPrivacyType.Public, true),
                cancellationToken);

        await _mediator.Send(new JoinCommunityCommand(name, null, userId), cancellationToken);
    }

    public async Task Handle(AddDiscordChannelToCommunityCommand request, CancellationToken cancellationToken)
    {
        var community = await LoadCommunity(request.CommunityName, request.InviteCode, cancellationToken);

        foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId).ToArray())
            community.Channels.Remove(existingChannel);

        var culture = SupportedCultures.NormalizeOrNull(request.Culture);
        community.Channels.Add(new Community.ChannelConfiguration(request.ChannelId, culture));
        await _communities.SaveCommunity(community, cancellationToken);

        await _bot.SendMessage(
            _localizer.Get(culture,
                "This channel was updated to receive notifications for the {0} community in PIU Scores!",
                (string)community.Name),
            request.ChannelId, cancellationToken);
    }

    public async Task Handle(CreateCommunityCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
        if (community != null) throw new CommunityAlreadyExistsException(request.CommunityName);
        community = new Community(request.CommunityName, userId, request.PrivacyType, false);
        community.MemberIds.Add(userId);
        await _communities.SaveCommunity(community,
            cancellationToken);
    }

    public async Task<Guid> Handle(CreateInviteLinkCommand request, CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.CommunityName, cancellationToken);
        if (!community.HasPermission(_currentUser.User.Id, CommunityPermission.ManageInviteLinks))
            throw new DeniedFromCommunityException(
                "You must have the invite-links permission to create invite links for this community");

        var newCode = Guid.NewGuid();
        community.InviteCodes[newCode] = request.ExpirationDate;
        await _communities.SaveCommunity(community, cancellationToken);
        return newCode;
    }

    public async Task<IEnumerable<CommunityLeaderboardRecord>> Handle(GetCommunityLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.Community, cancellationToken);
        if (community.PrivacyType == CommunityPrivacyType.Private && !(_currentUser.IsLoggedIn &&
                                                                       community.MemberIds.Contains(_currentUser
                                                                           .User.Id)))
            throw new DeniedFromCommunityException("This community is private and you must be a member to view it");

        return await _communities.GetLeaderboard(request.Mix, request.Community, cancellationToken);
    }

    public async Task<Community> Handle(GetCommunityQuery request, CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.CommunityName, cancellationToken);
        if (community.PrivacyType == CommunityPrivacyType.Private &&
            !(_currentUser.IsLoggedIn && community.MemberIds.Contains(_currentUser.User.Id)))
            throw new CommunityNotFoundException();

        return community;
    }

    public async Task<IEnumerable<CommunityOverviewRecord>> Handle(GetMyCommunitiesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.IsLoggedIn)
            return await _communities.GetCommunities(_currentUser.User.Id, cancellationToken);

        return await _communities.GetPublicCommunities(cancellationToken);
    }

    public async Task<IEnumerable<UserPhoenixScore>> Handle(GetPhoenixRecordsForCommunityQuery request,
        CancellationToken cancellationToken)
    {
        var community = await GetCommunity(request.CommuityName, cancellationToken);
        return await _scores.GetPhoenixScores(request.Mix, community.MemberIds, request.ChartId,
            cancellationToken);
    }

    public async Task<IEnumerable<CommunityOverviewRecord>> Handle(GetPublicCommunitiesQuery request,
        CancellationToken cancellationToken)
    {
        return await _communities.GetPublicCommunities(cancellationToken);
    }

    public async Task<int> Handle(GetCommunityCountQuery request, CancellationToken cancellationToken)
    {
        return await _communities.CountNonRegionalCommunities(cancellationToken);
    }

    public async Task<IEnumerable<Guid>> Handle(GetCommunityMembersQuery request,
        CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken);
        return community?.MemberIds ?? (IEnumerable<Guid>)Array.Empty<Guid>();
    }

    public async Task Handle(JoinCommunityByInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByInviteCode(request.InviteCode, cancellationToken);
        if (community == null) throw new CommunityNotFoundException();
        await Handle(new JoinCommunityCommand(community.Value, request.InviteCode), cancellationToken);
    }

    public async Task<Contracts.CommunityInvitePreviewRecord?> Handle(GetCommunityInvitePreviewQuery request,
        CancellationToken cancellationToken)
    {
        var communityName = await _communities.GetCommunityByInviteCode(request.InviteCode, cancellationToken);
        if (communityName == null) return null;
        var community = await _communities.GetCommunityByName(communityName.Value, cancellationToken);
        if (community == null) return null;

        community.InviteCodes.TryGetValue(request.InviteCode, out var expiration);
        var today = new DateOnly(_dateTime.Now.Year, _dateTime.Now.Month, _dateTime.Now.Day);
        var userId = _currentUser.IsLoggedIn ? _currentUser.User.Id : (Guid?)null;
        return new Contracts.CommunityInvitePreviewRecord(
            community.Name,
            community.PrivacyType,
            community.MemberIds.Count,
            expiration,
            expiration < today,
            userId != null && community.IsBanned(userId.Value),
            userId != null && community.MemberIds.Contains(userId.Value));
    }

    public async Task Handle(JoinCommunityCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? _currentUser.User.Id;
        var community = await GetCommunity(request.CommunityName, cancellationToken);

        // A retained ban row blocks both public join and invite-code join.
        if (community.IsBanned(userId))
            throw new DeniedFromCommunityException("You have been banned from this community");

        if (community.MemberIds.Contains(userId)) return;

        switch (community.PrivacyType)
        {
            case CommunityPrivacyType.Public:
                community.MemberIds.Add(userId);
                break;
            case CommunityPrivacyType.Private:
            case CommunityPrivacyType.PublicWithCode:
                var code = request.InviteCode ??
                           throw new DeniedFromCommunityException("This community requires an invite code");
                if (!community.InviteCodes.ContainsKey(code))
                    throw new DeniedFromCommunityException(
                        "This is not a valid community code for this community.");

                if (community.InviteCodes.TryGetValue(code, out var expirationDate) && expirationDate <
                    new DateOnly(_dateTime.Now.Year, _dateTime.Now.Month, _dateTime.Now.Day))
                    throw new DeniedFromCommunityException("This invite code is expired");

                community.MemberIds.Add(userId);
                break;
            default:
                throw new DeniedFromCommunityException("Community privacy type could not be determined");
        }

        await _communities.SaveCommunity(community, cancellationToken);
    }

    public async Task Handle(LeaveCommunityCommand request, CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? _currentUser.User.Id;
        var community = await GetCommunity(request.CommunityName, cancellationToken);
        if (!community.MemberIds.Contains(userId)) return;

        community.MemberIds.Remove(userId);
        await _communities.SaveCommunity(community, cancellationToken);
    }

    public async Task Handle(RemoveDiscordChannelFromCommunityCommand request, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(request.CommunityName, cancellationToken) ??
                        throw new CommunityNotFoundException();

        // The goodbye posts in the language the channel had registered.
        var culture = community.Channels
            .FirstOrDefault(c => c.ChannelId == request.ChannelId)?.Culture;
        foreach (var existingChannel in community.Channels.Where(c => c.ChannelId == request.ChannelId).ToArray())
            community.Channels.Remove(existingChannel);

        await _communities.SaveCommunity(community, cancellationToken);

        await _bot.SendMessage(
            _localizer.Get(culture,
                "This channel no longer receives notifications for the {0} community in PIU Scores",
                (string)community.Name),
            request.ChannelId, cancellationToken);
    }

    private async Task<Community> GetCommunity(Name name, CancellationToken cancellationToken)
    {
        var community = await _communities.GetCommunityByName(name, cancellationToken);
        return community ?? throw new CommunityNotFoundException();
    }

    private async Task<Community> LoadCommunity(Name? communityName, Guid? inviteCode,
        CancellationToken cancellationToken)
    {
        if (inviteCode != null)
            communityName = await _communities.GetCommunityByInviteCode(inviteCode.Value, cancellationToken) ??
                            throw new CommunityNotFoundException();

        if (communityName == null)
            throw new InvalidOperationException("Community Name must be provided if invite code is not used");

        var community = await _communities.GetCommunityByName(communityName.Value, cancellationToken) ??
                        throw new CommunityNotFoundException();
        if (community.RequiresCode && (inviteCode == null || !community.InviteCodes.ContainsKey(inviteCode.Value) ||
                                       community.InviteCodes[inviteCode.Value] <
                                       new DateOnly(_dateTime.Now.Year, _dateTime.Now.Month,
                                           _dateTime.Now.Day)))
            throw new CommunityNotFoundException();

        return community;
    }

    // Phoenix stays unprefixed — it is today's default context; the prefix marks the
    // new mix while both run in parallel (plan doc: "[Phoenix 2]" Discord prefix).
    private static string MixPrefix(MixEnum mix)
    {
        return mix == MixEnum.Phoenix ? string.Empty : "[" + mix.GetName() + "] ";
    }

    // Every community fan-out renders once per registered channel language: channels group
    // by culture, the render callback composes for each group, and the data behind the
    // callback was gathered once by the caller.
    private async Task SendToCommunityDiscords(Guid userId, Func<string?, string> render,
        CancellationToken cancellationToken)
    {
        foreach (var group in (await GetCommunityChannels(userId, cancellationToken)).GroupBy(c => c.Culture))
            await _bot.SendMessages(new[] { render(group.Key) }, group.Select(c => c.ChannelId).ToArray(),
                cancellationToken);
    }

    private async Task SendRichToCommunityDiscords(Guid userId,
        Func<string?, IReadOnlyList<RichBotMessage>> render, CancellationToken cancellationToken)
    {
        var channels = await GetCommunityChannels(userId, cancellationToken);
        if (!channels.Any()) return;
        foreach (var group in channels.GroupBy(c => c.Culture))
        {
            var messages = render(group.Key);
            if (!messages.Any()) continue;
            await _bot.SendRichMessages(messages, group.Select(c => c.ChannelId).ToArray(), cancellationToken);
        }
    }

    private async Task<IReadOnlyList<DiscordFeedChannel>> GetCommunityChannels(Guid userId,
        CancellationToken cancellationToken)
    {
        var communities =
            await _communities.GetCommunities(userId, cancellationToken);
        var channels = new List<DiscordFeedChannel>();
        foreach (var communityName in communities.Select(c => c.CommunityName))
        {
            var community = await _communities.GetCommunityByName(communityName, cancellationToken);
            if (community == null) continue;

            channels.AddRange(community.Channels.Select(c => new DiscordFeedChannel(c.ChannelId, c.Culture)));
        }

        // A channel reachable through two communities posts once; the first registration's
        // language wins.
        return channels.GroupBy(c => c.ChannelId).Select(g => g.First()).ToList();
    }

    public async Task Consume(ConsumeContext<UcsLeaderboardPlacedEvent> context)
    {
        var user = await _users.GetUser(context.Message.UserId);
        if (user == null) return;
        var placed = context.Message;
        await SendToCommunityDiscords(context.Message.UserId, culture => _localizer.Get(culture,
                "{0} scored {1} {2} on {3}'s {4} {5} UCS",
                user.Name, placed.Score,
                $"#LETTERGRADE|{PhoenixScore.From(placed.Score).LetterGrade}|{placed.IsBroken}#",
                placed.Artist, placed.SongName, $"#DIFFICULTY|{placed.Difficulty}#"),
            context.CancellationToken);
    }
}