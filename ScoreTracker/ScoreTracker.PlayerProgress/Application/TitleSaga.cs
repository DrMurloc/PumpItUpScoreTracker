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

    public sealed record ProcessTitles(Guid UserId) : IRequest;

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
        if (request.Mix == MixEnum.XX)
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

        ISet<Name> completedTitles;
        IEnumerable<RecordedPhoenixScore> scores;
        if (_currentUser.IsLoggedIn)
        {
            var userId = _currentUser.User.Id;
            completedTitles = (await _titles.GetCompletedTitles(userId, cancellationToken)).Select(t => t.Title)
                .ToHashSet();
            // Phoenix until per-mix computation lands (plan doc, saga commit).
            scores = await _phoenixScores.GetBestScores(MixEnum.Phoenix, userId, cancellationToken);
        }
        else
        {
            scores = Array.Empty<RecordedPhoenixScore>();
            completedTitles = new HashSet<Name>();
        }

        var charts = (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        return PhoenixTitleList.BuildProgress(charts, scores, completedTitles);
    }

    private async Task<IEnumerable<TitleProgress>> GetPhoenixProgress(Guid userId, CancellationToken cancellationToken)
    {
        // Phoenix until per-mix computation lands (plan doc, saga commit).
        var scores = await _phoenixScores.GetBestScores(MixEnum.Phoenix, userId, cancellationToken);
        var completed = (await _titles.GetCompletedTitles(userId, cancellationToken)).Select(t => t.Title).ToHashSet();
        var charts = (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        return PhoenixTitleList.BuildProgress(charts, scores, completed);
    }

    public async Task Consume(ConsumeContext<TitlesDetectedEvent> context)
    {
        await ProcessCharts(context.Message.UserId, context.Message.TitlesFound.Select(Name.From),
            context.CancellationToken);
    }

    private ParagonLevel GetLevel(TitleProgress tp)
    {
        return tp is PhoenixTitleProgress pt ? pt.ParagonLevel : ParagonLevel.None;
    }

    private async Task ProcessCharts(Guid userId, IEnumerable<Name> newCharts, CancellationToken cancellationToken)
    {
        var existingTitles = (await _titles.GetCompletedTitles(userId, cancellationToken))
            .ToDictionary(t => t.Title);
        var titleProgress = (await GetPhoenixProgress(userId, cancellationToken)).ToArray();
        var newTitlesHash = newCharts.Distinct().ToHashSet();
        foreach (var title in titleProgress)
            if (newTitlesHash.Contains(title.Title.Name))
                title.Complete();

        var allCompleted = titleProgress.Where(t => t.IsComplete)
            .Select(t => new TitleAchievedRecord(userId, t.Title.Name, GetLevel(t))).ToArray();

        await _titles.SaveTitles(userId, allCompleted, cancellationToken);

        var highest = allCompleted.Select(t => PhoenixTitleList.GetTitleByName(t.Title))
            .Where(t => t is PhoenixDifficultyTitle).Cast<PhoenixDifficultyTitle>()
            .OrderByDescending(d => (int)d.Level)
            .ThenByDescending(d => d.RequiredRating)
            .FirstOrDefault();
        if (highest != null)
            await _titles.SetHighestDifficultyTitle(userId, highest.Name, highest.Level, cancellationToken);


        var newCompleted = allCompleted.Where(c => !existingTitles.ContainsKey(c.Title))
            .Select(c => c.Title.ToString()).ToArray();
        var upgraded = allCompleted.Where(c =>
            existingTitles.ContainsKey(c.Title) && existingTitles[c.Title].ParagonLevel != c.ParagonLevel).ToArray();

        if (newCompleted.Any() || upgraded.Any())
            await _bus.Publish(
                new NewTitlesAcquiredEvent(userId, newCompleted,
                    upgraded.ToDictionary(t => t.Title.ToString(), t => t.ParagonLevel.ToString())),
                cancellationToken);
    }

    public async Task Consume(ConsumeContext<PlayerScoresUpdatedEvent> context)
    {
        await ProcessCharts(context.Message.UserId, Array.Empty<Name>(), context.CancellationToken);
    }

    public async Task Handle(ProcessTitles request, CancellationToken cancellationToken)
    {
        await ProcessCharts(request.UserId, Array.Empty<Name>(), cancellationToken);
    }
}