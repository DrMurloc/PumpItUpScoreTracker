﻿using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class AutoBuildSessionHandler : IRequestHandler<AutoBuildSessionQuery, TournamentSession>
    {
        private readonly IChartRepository _charts;
        private readonly IPhoenixRecordRepository _phoenixRecords;

        public AutoBuildSessionHandler(IChartRepository charts, IPhoenixRecordRepository phoenixRecords)
        {
            _charts = charts;
            _phoenixRecords = phoenixRecords;
        }

        public async Task<TournamentSession> Handle(AutoBuildSessionQuery request, CancellationToken cancellationToken)
        {
            var charts = (await _charts.GetCharts(MixEnum.Phoenix, cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);

            var orderedScores = (await _phoenixRecords.GetRecordedScores(request.UserId, cancellationToken))
                .Where(s => s is { Score: not null, Plate: not null } &&
                            request.Configuration.Scoring.GetScore(charts[s.ChartId], s.Score!.Value, s.Plate!.Value,
                                s.IsBroken) > 0)
                .OrderBy(r =>
                    charts[r.ChartId].Song.Duration /
                    request.Configuration.Scoring.GetScore(charts[r.ChartId], r.Score!.Value, r.Plate!.Value,
                        r.IsBroken));

            var session = new TournamentSession(request.UserId, request.Configuration);

            foreach (var score in orderedScores)
            {
                var chart = charts[score.ChartId];
                if (!session.CanAdd(chart) || session.AverageTimeWithAddedChart(chart) < request.MinimumRestPerChart)
                {
                    continue;
                }

                session.Add(chart, score.Score!.Value, score.Plate!.Value, score.IsBroken);
            }

            return session;
        }
    }
}