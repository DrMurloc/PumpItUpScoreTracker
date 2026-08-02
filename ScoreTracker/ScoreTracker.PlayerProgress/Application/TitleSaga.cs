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

    private readonly IPlayerScoreBatchAccumulator _batches;
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
        IPlayerScoreBatchAccumulator batches,
        IBus bus)
    {
        _batches = batches;
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
        // (CompletionRequired == 0): events, play/plate counts, staff. Rather than announce them
        // itself, it parks them on the open score batch so the ONE snapshot card carries them
        // alongside the scores. This is safe on timing, not luck: the import publishes this
        // event immediately after its last score save, and that batch does not drain for
        // another two minutes.
        //
        // It deliberately does NOT read the session's milestone rows to find them — that is what
        // made every card in a session repeat the previous ones' titles, since a session
        // envelope spans 8 hours while a batch drains after 2 minutes.
        var e = context.Message;
        var minted = await ProcessCharts(e.Mix, e.UserId, e.TitlesFound.Select(Name.From),
            context.CancellationToken, sessionId: e.SessionId,
            announceLegacy: false, siteOnlyAnnounce: true);
        if (minted.Count == 0) return;

        var completed = minted.Where(m => m.Kind == MilestoneKind.TitleCompleted)
            .Select(m => m.Title!)
            .ToArray();
        if (_batches.TryAddDetectedTitles(e.Mix, e.UserId, completed)) return;

        // No batch to carry them — an import that saved no scores at all, so no snapshot card is
        // coming. They get their own card, exactly as before. Paragon levels are retired, so the
        // event's upgrade map is always empty now.
        await _bus.Publish(
            new NewTitlesAcquiredEvent(e.UserId, completed, new Dictionary<string, string>(), e.Mix, e.SessionId),
            context.CancellationToken);
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

        // Title completions become timestamped milestones — UserTitle rows have no acquisition
        // date, so this is the only record of WHEN. Paragon upgrades no longer produce one:
        // per-level progress is folder standings now, which every player has rather than only
        // those who already finished a title (docs/design/folder-level-progression.md §5.2).
        // The legacy NewTitlesAcquiredEvent below still carries them, unchanged.
        var writes = newCompleted
            .Select(t => new PlayerMilestoneWrite(MilestoneKind.TitleCompleted, sessionId, _dateTime.Now,
                Title: t))
            .ToArray();
        if (writes.Length > 0) await _milestones.Append(mix, userId, writes, cancellationToken);
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

        // The card shows the completions THIS BATCH crossed, and only those. Reading the whole
        // session back out of the milestone table instead made every card in a session repeat
        // the ones before it: a session envelope lasts 8 hours while a score batch drains after
        // 2 minutes, so one session emits many batches — and each card re-announced every title
        // earned since the session opened.
        var crossings = await ComputeBatchCompletions(request, cancellationToken);

        // Plus whatever the site path parked on this batch. Taking them removes them, so the next
        // batch in the same session cannot announce them again. They are already persisted as
        // milestones by the site path — these records exist only to reach the card. Site badges
        // carry no requirement to climb, so they trail the ladders, alphabetically.
        var badges = _batches.TakeDetectedTitles(request.Mix, request.UserId)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .Select(t => new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, request.SessionId,
                _dateTime.Now, null, null, t, null));

        var progress = await ComputeProgressDeltas(request, cancellationToken);
        await PersistProgress(request, progress, cancellationToken);
        return new SessionTitlesResult(crossings.Concat(badges).ToArray(), progress);
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
            // Floor-aware percent (PercentComplete), so a ladder rung the player hasn't reached
            // reads 0% instead of raw count/required — otherwise every rung above the active one
            // shows spurious progress and the whole ladder appears to move at once.
            .Select(t => new TitleProgressDelta(t.Title.Name,
                beforeByTitle.TryGetValue(t.Title.Name, out var b) ? b.PercentComplete : 0,
                t.PercentComplete, ScopeOf(t), t.CompletionCount, t.Title.CompletionRequired))
            .Where(d => (int)(d.NewPercent * 100) > (int)(d.OldPercent * 100))
            .OrderByDescending(d => d.NewPercent)
            .Take(5)
            .ToArray();
    }

    /// <summary>
    ///     What a progress bar gets drawn per. Phoenix difficulty titles are keyed on the LEVEL
    ///     alone — <c>CompletionProgress</c> accepts any chart at that level, single or double —
    ///     so "21" is the scope and there is no S21/D21 split to make. Phoenix 2 titles gate on
    ///     a pool instead, which is not a folder at all.
    /// </summary>
    private static string ScopeOf(TitleProgress progress)
    {
        return (progress as PhoenixTitleProgress)?.PhoenixTitle switch
        {
            Phoenix2PumbilityTitle pumbility => pumbility.Pool.ToString(),
            PhoenixDifficultyTitle difficulty => ((int)difficulty.Level).ToString(),
            _ => string.Empty
        };
    }

    /// <summary>
    ///     Persists the batch's progress movements so the Sessions page can draw them — the
    ///     event payload dies with the message, and the page renders long after. Only scoped
    ///     deltas are stored: a title with no level or pool has no bar to draw, so a row for it
    ///     would be weight the page never reads.
    /// </summary>
    private async Task PersistProgress(CaptureSessionTitles request, IReadOnlyList<TitleProgressDelta> deltas,
        CancellationToken cancellationToken)
    {
        var writes = deltas
            .Where(d => d.Scope.Length > 0)
            .Select(d => new PlayerMilestoneWrite(MilestoneKind.TitleProgress, request.SessionId, _dateTime.Now,
                d.OldPercent, d.NewPercent, d.Title,
                $"{d.Scope}|{(int)d.Current}|{(int)d.Required}"))
            .ToArray();
        if (writes.Length == 0) return;
        await _milestones.Append(request.Mix, request.UserId, writes, cancellationToken);
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
                     .Where(t => t.Title.CompletionRequired > 0 && t.IsComplete)
                     .OrderBy(LadderOrder)
                     .ThenBy(t => t.Title.CompletionRequired)
                     .ThenBy(t => t.Title.Name.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            var wasBefore = beforeByTitle.GetValueOrDefault(title.Title.Name);
            var newlyComplete = wasBefore is not { IsComplete: true };
            if (newlyComplete)
                writes.Add(new PlayerMilestoneWrite(MilestoneKind.TitleCompleted, request.SessionId, _dateTime.Now,
                    Title: title.Title.Name.ToString()));
        }

        if (writes.Count == 0) return Array.Empty<PlayerMilestoneRecord>();
        await _milestones.Append(request.Mix, request.UserId, writes, cancellationToken);
        return writes.Select(w => new PlayerMilestoneRecord(w.Kind, w.SessionId, w.OccurredAt, w.OldValue,
            w.NewValue, w.Title, w.Detail)).ToList();
    }

    /// <summary>
    ///     Which ladder a title belongs to, for grouping a batch's completions. Completions are
    ///     minted ladder-by-ladder with the lowest rung first, so a card reads like the climb it
    ///     was — five [D] rungs in a row rather than SQL's arbitrary order. The PUMBILITY pools
    ///     lead in the same order as the stat lines above them (total, then singles, doubles).
    /// </summary>
    private static int LadderOrder(TitleProgress progress)
    {
        return (progress as PhoenixTitleProgress)?.PhoenixTitle switch
        {
            Phoenix2PumbilityTitle { Pool: PumbilityPool.Total } => 0,
            Phoenix2PumbilityTitle { Pool: PumbilityPool.Singles } => 1,
            Phoenix2PumbilityTitle { Pool: PumbilityPool.Doubles } => 2,
            PhoenixDifficultyTitle => 3,
            PhoenixSkillTitle => 4,
            _ => 5
        };
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