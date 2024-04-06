﻿using MassTransit;
using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.XX;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class TitleSaga : IRequestHandler<GetTitleProgressQuery, IEnumerable<TitleProgress>>,
    IConsumer<TitlesDetectedEvent>,
    IConsumer<PlayerScoreUpdatedEvent>
{
    private readonly IXXChartAttemptRepository _chartAttempts;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPhoenixRecordRepository _phoenixScores;
    private readonly ITitleRepository _titles;
    private readonly IBus _bus;

    public TitleSaga(ICurrentUserAccessor currentUser,
        IXXChartAttemptRepository chartAttempts,
        IPhoenixRecordRepository phoenixScores,
        IChartRepository charts,
        ITitleRepository titles,
        IBus bus)
    {
        _currentUser = currentUser;
        _chartAttempts = chartAttempts;
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
                attempts = await _chartAttempts.GetBestAttempts(userId, cancellationToken);
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
            completedTitles = (await _titles.GetCompletedTitles(userId, cancellationToken)).ToHashSet();
            scores = await _phoenixScores.GetRecordedScores(userId, cancellationToken);
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
        var scores = await _phoenixScores.GetRecordedScores(userId, cancellationToken);
        var completed = (await _titles.GetCompletedTitles(userId, cancellationToken)).ToHashSet();
        var charts = (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        return PhoenixTitleList.BuildProgress(charts, scores, completed);
    }

    public async Task Consume(ConsumeContext<TitlesDetectedEvent> context)
    {
        await ProcessCharts(context.Message.UserId, context.Message.TitlesFound.Select(Name.From),
            context.CancellationToken);
    }

    private async Task ProcessCharts(Guid userId, IEnumerable<Name> newCharts, CancellationToken cancellationToken)
    {
        var existingTitles = (await _titles.GetCompletedTitles(userId, cancellationToken))
            .ToHashSet();
        var titleProgress = (await GetPhoenixProgress(userId, cancellationToken)).ToArray();
        var newTitlesHash = newCharts.Distinct().ToHashSet();
        foreach (var title in titleProgress)
            if (newTitlesHash.Contains(title.Title.Name))
                title.Complete();

        var allCompleted = titleProgress.Where(t => t.IsComplete).Select(t => t.Title.Name).ToArray();

        await _titles.SaveTitles(userId, allCompleted, cancellationToken);

        var newCompleted = allCompleted.Where(c => !existingTitles.Contains(c)).ToArray();

        if (newCompleted.Any())
            await _bus.Publish(
                new NewTitlesAcquiredEvent(userId, newCompleted.Select(t => t.ToString())),
                cancellationToken);
    }

    public async Task Consume(ConsumeContext<PlayerScoreUpdatedEvent> context)
    {
        await ProcessCharts(context.Message.UserId, Array.Empty<Name>(), context.CancellationToken);
    }
}