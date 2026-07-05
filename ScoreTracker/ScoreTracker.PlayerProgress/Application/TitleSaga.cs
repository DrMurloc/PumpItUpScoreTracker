using MassTransit;
using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Models.Titles.XX;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Application;

internal sealed class TitleSaga : IRequestHandler<GetTitleProgressQuery, IEnumerable<TitleProgress>>,
    IConsumer<TitlesDetectedEvent>,
    IConsumer<PlayerScoresUpdatedEvent>,
    IRequestHandler<TitleSaga.ProcessTitles>
{
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreReader _phoenixScores;
    private readonly ITitleRepository _titles;
    private readonly IBus _bus;

    public sealed record ProcessTitles(Guid UserId, MixEnum Mix = MixEnum.Phoenix) : IRequest;

    public TitleSaga(ICurrentUserAccessor currentUser,
        IScoreReader phoenixScores,
        IChartRepository charts,
        ITitleRepository titles,
        IBus bus)
    {
        _currentUser = currentUser;
        _phoenixScores = phoenixScores;
        _charts = charts;
        _titles = titles;
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

                // Phoenix2's list is deliberately EMPTY at launch (locked decision), so its
                // progress is always an empty collection until the real list is known.
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
        await ProcessCharts(context.Message.Mix, context.Message.UserId,
            context.Message.TitlesFound.Select(Name.From),
            context.CancellationToken);
    }

    private ParagonLevel GetLevel(TitleProgress tp)
    {
        return tp is PhoenixTitleProgress pt ? pt.ParagonLevel : ParagonLevel.None;
    }

    private async Task ProcessCharts(MixEnum mix, Guid userId, IEnumerable<Name> newCharts,
        CancellationToken cancellationToken)
    {
        var existingTitles = (await _titles.GetCompletedTitles(mix, userId, cancellationToken))
            .ToDictionary(t => t.Title);
        // A Phoenix2 score event simply produces zero titles here — the mix's list is empty.
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


        var newCompleted = allCompleted.Where(c => !existingTitles.ContainsKey(c.Title))
            .Select(c => c.Title.ToString()).ToArray();
        var upgraded = allCompleted.Where(c =>
            existingTitles.ContainsKey(c.Title) && existingTitles[c.Title].ParagonLevel != c.ParagonLevel).ToArray();

        if (newCompleted.Any() || upgraded.Any())
            await _bus.Publish(
                new NewTitlesAcquiredEvent(userId, newCompleted,
                    upgraded.ToDictionary(t => t.Title.ToString(), t => t.ParagonLevel.ToString()),
                    mix),
                cancellationToken);
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

    public async Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        await ProcessCharts(context.Message.Mix, context.Message.UserId, Array.Empty<Name>(),
            context.CancellationToken);
    }

    public async Task Handle(ProcessTitles request, CancellationToken cancellationToken)
    {
        await ProcessCharts(request.Mix, request.UserId, Array.Empty<Name>(), cancellationToken);
    }
}