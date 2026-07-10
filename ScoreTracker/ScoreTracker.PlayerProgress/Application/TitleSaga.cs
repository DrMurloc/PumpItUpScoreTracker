using MassTransit;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Interface;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Models.Titles.XX;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

internal sealed class TitleSaga : IRequestHandler<GetTitleProgressQuery, IEnumerable<TitleProgress>>,
    IConsumer<TitlesDetectedEvent>,
    IRequestHandler<TitleSaga.ProcessTitles>,
    IRequestHandler<TitleSaga.CaptureSessionTitles, TitleSaga.SessionTitlesResult>
{
    /// <summary>
    ///     The title step of the session-snapshot pipeline: processes completions and
    ///     paragon gains for the batch (WITHOUT the legacy Discord announcement — the
    ///     snapshot card carries them) and computes the per-title progress deltas from
    ///     the batch's old→new scores. Dispatched in-process by the capture
    ///     orchestrator; this saga no longer consumes the raw score event. The legacy
    ///     announcement survives only on the <see cref="TitlesDetectedEvent" /> path —
    ///     titles granted by the official site have no session, so no card covers them.
    /// </summary>
    public sealed record CaptureSessionTitles(Guid UserId, MixEnum Mix, Guid? SessionId,
        IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange> Changes) : IRequest<SessionTitlesResult>;

    public sealed record SessionTitlesResult(
        IReadOnlyList<PlayerMilestoneRecord> Milestones, IReadOnlyList<TitleProgressDelta> Progress);

    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreReader _phoenixScores;
    private readonly ITitleRepository _titles;
    private readonly IPlayerMilestoneRepository _milestones;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IBus _bus;

    public sealed record ProcessTitles(Guid UserId, MixEnum Mix = MixEnum.Phoenix) : IRequest;

    public TitleSaga(ICurrentUserAccessor currentUser,
        IScoreReader phoenixScores,
        IChartRepository charts,
        ITitleRepository titles,
        IPlayerMilestoneRepository milestones,
        IDateTimeOffsetAccessor dateTime,
        IBus bus)
    {
        _currentUser = currentUser;
        _phoenixScores = phoenixScores;
        _charts = charts;
        _titles = titles;
        _milestones = milestones;
        _dateTime = dateTime;
        _bus = bus;
    }

    public async Task<IEnumerable<TitleProgress>> Handle(GetTitleProgressQuery request,
        CancellationToken cancellationToken)
    {
        // Explicit three-way dispatch — no "not XX ⇒ Phoenix" fallthrough. An unknown
        // mix must throw loudly rather than silently show Phoenix titles (plan doc).
        switch (request.Mix)
        {
            case MixEnum.XX:
            {
                IEnumerable<BestXXChartAttempt> attempts;
                if (_currentUser.IsLoggedIn)
                {
                    var userId = _currentUser.User.Id;
                    attempts = await _phoenixScores.GetBestXXAttempts(userId, cancellationToken);
                }
                else
                {
                    attempts = Array.Empty<BestXXChartAttempt>();
                }

                return XXTitleList.BuildProgress(attempts);
            }
            case MixEnum.Phoenix:
            case MixEnum.Phoenix2:
            {
                ISet<Name> completedTitles;
                IEnumerable<RecordedPhoenixScore> scores;
                if (_currentUser.IsLoggedIn)
                {
                    var userId = _currentUser.User.Id;
                    completedTitles = (await _titles.GetCompletedTitles(request.Mix, userId, cancellationToken))
                        .Select(t => t.Title)
                        .ToHashSet();
                    scores = await _phoenixScores.GetBestScores(request.Mix, userId, cancellationToken);
                }
                else
                {
                    scores = Array.Empty<RecordedPhoenixScore>();
                    completedTitles = new HashSet<Name>();
                }

                var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
                    .ToDictionary(c => c.Id);

                return request.Mix == MixEnum.Phoenix
                    ? PhoenixTitleList.BuildProgress(charts, scores, completedTitles)
                    : Phoenix2TitleList.BuildProgress(charts, scores, completedTitles);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(request.Mix), request.Mix,
                    "No title list is known for this mix");
        }
    }

    private async Task<IEnumerable<TitleProgress>> GetProgress(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var scores = await _phoenixScores.GetBestScores(mix, userId, cancellationToken);
        var completed = (await _titles.GetCompletedTitles(mix, userId, cancellationToken)).Select(t => t.Title)
            .ToHashSet();
        var charts = (await _charts.GetCharts(mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        return mix switch
        {
            MixEnum.Phoenix => PhoenixTitleList.BuildProgress(charts, scores, completed),
            MixEnum.Phoenix2 => Phoenix2TitleList.BuildProgress(charts, scores, completed),
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix,
                "Title persistence only exists for Phoenix-generation mixes")
        };
    }

    public async Task Consume(ConsumeContext<TitlesDetectedEvent> context)
    {
        // The site path owns only the badges the score pipeline can't compute
        // (CompletionRequired == 0): events, play/plate counts, staff. When the detecting
        // import also saved scores, these completions are attributed to that session and ride
        // its snapshot card (no legacy announce — the card carries them); when it saved none,
        // SessionId is null and they get their own announcement. Either way everything is
        // saved — the DB stays correct.
        var sessionId = context.Message.SessionId;
        await ProcessCharts(context.Message.Mix, context.Message.UserId,
            context.Message.TitlesFound.Select(Name.From),
            context.CancellationToken, sessionId: sessionId,
            announceLegacy: sessionId == null, siteOnlyAnnounce: true);
    }

    private ParagonLevel GetLevel(TitleProgress tp)
    {
        return tp is PhoenixTitleProgress pt ? pt.ParagonLevel : ParagonLevel.None;
    }

    private async Task<IReadOnlyList<PlayerMilestoneRecord>> ProcessCharts(MixEnum mix, Guid userId,
        IEnumerable<Name> newCharts, CancellationToken cancellationToken, Guid? sessionId = null,
        bool announceLegacy = true, bool mint = true, bool siteOnlyAnnounce = false)
    {
        var existingTitles = (await _titles.GetCompletedTitles(mix, userId, cancellationToken))
            .ToDictionary(t => t.Title);
        var titleProgress = (await GetProgress(mix, userId, cancellationToken)).ToArray();
        var newTitlesHash = newCharts.Distinct().ToHashSet();
        foreach (var title in titleProgress)
            if (newTitlesHash.Contains(title.Title.Name))
                title.Complete();

        var allCompleted = titleProgress.Where(t => t.IsComplete)
            .Select(t => new TitleAchievedRecord(userId, t.Title.Name, GetLevel(t))).ToArray();

        await _titles.SaveTitles(mix, userId, allCompleted, cancellationToken);

        var highest = allCompleted.Select(t => GetTitleByName(mix, t.Title))
            .Where(t => t is PhoenixDifficultyTitle).Cast<PhoenixDifficultyTitle>()
            .OrderByDescending(d => (int)d.Level)
            .ThenByDescending(d => d.RequiredRating)
            .FirstOrDefault();
        if (highest != null)
            await _titles.SetHighestDifficultyTitle(mix, userId, highest.Name, highest.Level, cancellationToken);

        // The score path (CaptureSessionTitles) persists through here but mints its own
        // milestones from the batch's before→after crossing, so it skips this tail.
        if (!mint) return Array.Empty<PlayerMilestoneRecord>();

        var newCompleted = allCompleted.Where(c => !existingTitles.ContainsKey(c.Title))
            .Select(c => c.Title.ToString()).ToArray();
        var upgraded = allCompleted.Where(c =>
            existingTitles.ContainsKey(c.Title) && existingTitles[c.Title].ParagonLevel != c.ParagonLevel).ToArray();

        // The site path announces only the badges the score pipeline can't compute.
        if (siteOnlyAnnounce)
        {
            newCompleted = newCompleted.Where(n => GetTitleByName(mix, Name.From(n)).CompletionRequired == 0)
                .ToArray();
            upgraded = upgraded.Where(u => GetTitleByName(mix, u.Title).CompletionRequired == 0).ToArray();
        }

        if (newCompleted.Length == 0 && upgraded.Length == 0) return Array.Empty<PlayerMilestoneRecord>();

        // Title completions and paragon gains become timestamped milestones —
        // UserTitle rows have no acquisition date, so this is the only record of WHEN.
        var writes = newCompleted
            .Select(t => new PlayerMilestoneWrite(MilestoneKind.TitleCompleted, sessionId, _dateTime.Now,
                Title: t))
            .Concat(upgraded.Select(t => new PlayerMilestoneWrite(MilestoneKind.ParagonLevelGain, sessionId,
                _dateTime.Now, Title: t.Title.ToString(), Detail: t.ParagonLevel.ToString())))
            .ToArray();
        await _milestones.Append(mix, userId, writes, cancellationToken);
        if (announceLegacy)
            await _bus.Publish(
                new NewTitlesAcquiredEvent(userId, newCompleted,
                    upgraded.ToDictionary(t => t.Title.ToString(), t => t.ParagonLevel.ToString()),
                    mix, sessionId),
                cancellationToken);
        return writes
            .Select(w => new PlayerMilestoneRecord(w.Kind, w.SessionId, w.OccurredAt, w.OldValue, w.NewValue,
                w.Title, w.Detail))
            .ToArray();
    }

    private static PhoenixTitle GetTitleByName(MixEnum mix, Name title)
    {
        return mix switch
        {
            MixEnum.Phoenix => PhoenixTitleList.GetTitleByName(title),
            MixEnum.Phoenix2 => Phoenix2TitleList.GetTitleByName(title),
            _ => throw new ArgumentOutOfRangeException(nameof(mix), mix,
                "Title persistence only exists for Phoenix-generation mixes")
        };
    }

    public async Task<SessionTitlesResult> Handle(CaptureSessionTitles request,
        CancellationToken cancellationToken)
    {
        // XX and unknown mixes have no title persistence — an empty result, not a throw
        // (the old raw-event consumer would have faulted on an XX score event).
        if (request.Mix is not (MixEnum.Phoenix or MixEnum.Phoenix2))
            return new SessionTitlesResult(Array.Empty<PlayerMilestoneRecord>(), Array.Empty<TitleProgressDelta>());

        // Persist the up-to-date title set (SaveTitles + highest) WITHOUT minting or
        // announcing — the card's title milestones come from this batch's before→after
        // crossing, which surfaces a completion even when the site-detection path already
        // saved the same title (it fires first during imports).
        await ProcessCharts(request.Mix, request.UserId, Array.Empty<Name>(), cancellationToken,
            request.SessionId, announceLegacy: false, mint: false);

        var scoreMilestones = await ComputeBatchCompletions(request, cancellationToken);
        var progress = await ComputeProgressDeltas(request, cancellationToken);

        // The card shows ALL of the session's title completions (owner call), so single-source
        // them from the milestone table by session: this folds in the site-detection path's
        // basic-badge completions (written against this same SessionId before the batch
        // drained) alongside the score-crossing ones just computed — no double-count, since the
        // two paths own disjoint title sets (CompletionRequired == 0 vs > 0). A null-session
        // batch (manual entry) has no site path, so it uses the score-crossing list directly.
        IReadOnlyList<PlayerMilestoneRecord> milestones = request.SessionId == null
            ? scoreMilestones
            : (await _milestones.GetMilestonesBySessions(request.UserId, new[] { request.SessionId.Value },
                    cancellationToken))
                .Where(m => m.Kind is MilestoneKind.TitleCompleted or MilestoneKind.ParagonLevelGain)
                .ToArray();
        return new SessionTitlesResult(milestones, progress);
    }

    /// <summary>
    ///     Per-title progress movement across the batch (design doc revision 2, owner
    ///     call: real deltas, not a summary line). The before-state is reconstructed
    ///     from the changes' old scores — a chart with no prior score drops out, an
    ///     upscored chart reverts to its old score (old plate isn't on the event, so
    ///     plate-based progress is approximated by the current plate). Only titles
    ///     whose ROUNDED percent actually moved make the list, nearest-to-complete
    ///     first, capped at 5 — the card shows at most 3. Chart-specific titles (skill,
    ///     boss-breaker) are excluded: they ride the per-row caption, not the top deltas.
    /// </summary>
    private async Task<IReadOnlyList<TitleProgressDelta>> ComputeProgressDeltas(CaptureSessionTitles request,
        CancellationToken cancellationToken)
    {
        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var completed = (await _titles.GetCompletedTitles(request.Mix, request.UserId, cancellationToken))
            .Select(t => t.Title).ToHashSet();
        var current = (await _phoenixScores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .ToArray();
        var before = BeforeScores(current, request.Changes);

        var beforeByTitle = BuildProgress(request.Mix, charts, before, completed)
            .ToDictionary(t => t.Title.Name);
        return BuildProgress(request.Mix, charts, current, completed)
            .Where(t => !t.IsComplete && t.Title.CompletionRequired > 0 && t.Title is not ISpecificChartTitle)
            .Select(t => new TitleProgressDelta(t.Title.Name,
                beforeByTitle.TryGetValue(t.Title.Name, out var b) ? Percent(b) : 0,
                Percent(t)))
            .Where(d => (int)(d.NewPercent * 100) > (int)(d.OldPercent * 100))
            .OrderByDescending(d => d.NewPercent)
            .Take(5)
            .ToArray();
    }

    // The batch's before-state: current scores with the changed charts reverted to their
    // old score (a chart with no prior score drops out; an upscored chart reverts, its plate
    // approximated by the current one).
    private static RecordedPhoenixScore[] BeforeScores(IReadOnlyList<RecordedPhoenixScore> current,
        IReadOnlyList<PlayerScoresUpdatedEvent.ScoreChange> changes)
    {
        var changed = changes.GroupBy(c => c.ChartId).ToDictionary(g => g.Key, g => g.First());
        return current
            .Where(s => !changed.TryGetValue(s.ChartId, out var c) || c.OldScore != null)
            .Select(s => changed.TryGetValue(s.ChartId, out var c)
                ? s with { Score = PhoenixScore.From(c.OldScore!.Value), IsBroken = c.IsNewPass }
                : s)
            .ToArray();
    }

    /// <summary>
    ///     Title completions and paragon gains this batch crossed, from the changes' old→new
    ///     scores against a SCORE-ONLY completion state (empty completed set) — so a fresh
    ///     crossing surfaces even when the site-detection path already persisted the title
    ///     (it fires first during imports). Mints the milestones the snapshot card renders,
    ///     chart-specific titles (skill, boss-breaker) included.
    /// </summary>
    private async Task<IReadOnlyList<PlayerMilestoneRecord>> ComputeBatchCompletions(CaptureSessionTitles request,
        CancellationToken cancellationToken)
    {
        var charts = (await _charts.GetCharts(request.Mix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);
        var current = (await _phoenixScores.GetBestScores(request.Mix, request.UserId, cancellationToken))
            .ToArray();
        var before = BeforeScores(current, request.Changes);

        var empty = (ISet<Name>)new HashSet<Name>();
        var beforeByTitle = BuildProgress(request.Mix, charts, before, empty).ToDictionary(t => t.Title.Name);

        var writes = new List<PlayerMilestoneWrite>();
        foreach (var title in BuildProgress(request.Mix, charts, current, empty)
                     .Where(t => t.Title.CompletionRequired > 0 && t.IsComplete))
        {
            var wasBefore = beforeByTitle.GetValueOrDefault(title.Title.Name);
            var newlyComplete = wasBefore is not { IsComplete: true };
            if (newlyComplete)
                writes.Add(new PlayerMilestoneWrite(MilestoneKind.TitleCompleted, request.SessionId, _dateTime.Now,
                    Title: title.Title.Name.ToString()));

            var newParagon = GetLevel(title);
            var oldParagon = wasBefore == null ? ParagonLevel.None : GetLevel(wasBefore);
            if (newParagon != ParagonLevel.None && (newlyComplete || newParagon > oldParagon))
                writes.Add(new PlayerMilestoneWrite(MilestoneKind.ParagonLevelGain, request.SessionId,
                    _dateTime.Now, Title: title.Title.Name.ToString(), Detail: newParagon.ToString()));
        }

        if (writes.Count == 0) return Array.Empty<PlayerMilestoneRecord>();
        await _milestones.Append(request.Mix, request.UserId, writes, cancellationToken);
        return writes.Select(w => new PlayerMilestoneRecord(w.Kind, w.SessionId, w.OccurredAt, w.OldValue,
            w.NewValue, w.Title, w.Detail)).ToList();
    }

    private static double Percent(TitleProgress progress)
    {
        return progress.Title.CompletionRequired <= 0
            ? 0
            : Math.Min(1.0, progress.CompletionCount / progress.Title.CompletionRequired);
    }

    private static IEnumerable<TitleProgress> BuildProgress(MixEnum mix, IDictionary<Guid, Chart> charts,
        IEnumerable<RecordedPhoenixScore> scores, ISet<Name> completed)
    {
        return mix == MixEnum.Phoenix
            ? PhoenixTitleList.BuildProgress(charts, scores, completed)
            : Phoenix2TitleList.BuildProgress(charts, scores, completed);
    }

    public async Task Handle(ProcessTitles request, CancellationToken cancellationToken)
    {
        await ProcessCharts(request.Mix, request.UserId, Array.Empty<Name>(), cancellationToken);
    }
}