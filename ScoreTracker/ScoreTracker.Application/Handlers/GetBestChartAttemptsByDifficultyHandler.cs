using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetBestChartAttemptsByDifficultyHandler : IRequestHandler<GetBestChartAttemptsByDifficultyQuery,
        IEnumerable<BestChartAttempt>>
{
    private readonly IChartAttemptRepository _chartAttemptRepository;
    private readonly IChartRepository _chartRepository;

    public GetBestChartAttemptsByDifficultyHandler(IChartAttemptRepository chartAttemptRepository,
        IChartRepository chartRepository)
    {
        _chartAttemptRepository = chartAttemptRepository;
        _chartRepository = chartRepository;
    }

    public async Task<IEnumerable<BestChartAttempt>> Handle(GetBestChartAttemptsByDifficultyQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<BestChartAttempt>();
        var charts = await _chartRepository.GetChartsByDifficulty(request.Difficulty, cancellationToken);
        foreach (var chart in charts)
        {
            var bestAttempt = await _chartAttemptRepository.GetBestAttempt(request.UserId, chart, cancellationToken);
            result.Add(new BestChartAttempt(chart, bestAttempt));
        }

        return result;
    }
}