using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Events;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Handlers
{
    public sealed class MatchSaga : IRequestHandler<GetMatchQuery, MatchView>,
        IRequestHandler<UpdateMatchCommand>,
        IRequestHandler<DrawChartsCommand>,
        IRequestHandler<ResolveMatchCommand>,
        IRequestHandler<GetAllMatchesQuery, IEnumerable<MatchView>>,
        IRequestHandler<SaveRandomSettingsCommand>,
        IRequestHandler<GetAllRandomSettingsQuery, IEnumerable<(Name name, RandomSettings settings)>>
    {
        private readonly IMatchRepository _matchRepository;
        private readonly IMediator _mediator;

        public MatchSaga(IMediator mediator, IMatchRepository matchRepository)
        {
            _matchRepository = matchRepository;
            _mediator = mediator;
        }

        public async Task<MatchView> Handle(GetMatchQuery request, CancellationToken cancellationToken)
        {
            return await _matchRepository.GetMatch(request.MatchName, cancellationToken);
        }

        public async Task<Unit> Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
        {
            await _matchRepository.SaveMatch(request.NewView, cancellationToken);
            await _mediator.Publish(new MatchUpdatedEvent(request.NewView), cancellationToken);
            return Unit.Value;
        }

        public async Task<Unit> Handle(DrawChartsCommand request, CancellationToken cancellationToken)
        {
            var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);
            var settings = await _matchRepository.GetRandomSettings(match.RandomSettings, cancellationToken);
            var charts = await _mediator.Send(new GetRandomChartsQuery(settings), cancellationToken);
            var newMatch = match with
            {
                ActiveCharts = charts.Select(c => c.Id).ToArray(), State = MatchState.CardDraw
            };
            await _matchRepository.SaveMatch(newMatch, cancellationToken);
            await _mediator.Publish(new MatchUpdatedEvent(newMatch), cancellationToken);
            return Unit.Value;
        }

        public async Task<Unit> Handle(ResolveMatchCommand request, CancellationToken cancellationToken)
        {
            var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);
            var scoring = match.Players.Length == 2
                ? new[] { 1, 0 }
                : match.Players.Select((p, i) => i + 1).OrderByDescending(i => i).ToArray();

            for (var chartIndex = 0; chartIndex < match.ActiveCharts.Length; chartIndex++)
            {
                var pointIndex = 0;
                foreach (var scoreGroup in match.Players.GroupBy(p => (int)match.Scores[p][chartIndex])
                             .OrderByDescending(g => g.Key))
                {
                    foreach (var player in scoreGroup) match.Points[player][chartIndex] = scoring[pointIndex];

                    pointIndex += scoreGroup.Count();
                }
            }

            var currentPosition = 0;

            foreach (var tie in match.Players.GroupBy(p => match.Points[p].Sum())
                         .OrderByDescending(g => g.Key))
            foreach (var tieBreakerResult in tie.OrderByDescending(name => match.Scores[name].Sum(s => s)))
                match.FinalPlaces[currentPosition++] = tieBreakerResult;

            var newMatchState = match with { State = MatchState.Completed };
            await _matchRepository.SaveMatch(newMatchState, cancellationToken);

            await _mediator.Publish(new MatchUpdatedEvent(newMatchState), cancellationToken);
            return Unit.Value;
        }

        public async Task<IEnumerable<MatchView>> Handle(GetAllMatchesQuery request,
            CancellationToken cancellationToken)
        {
            return await _matchRepository.GetAllMatches(cancellationToken);
        }

        public async Task<Unit> Handle(SaveRandomSettingsCommand request, CancellationToken cancellationToken)
        {
            await _matchRepository.SaveRandomSettings(request.SettingsName, request.Settings, cancellationToken);
            return Unit.Value;
        }

        public async Task<IEnumerable<(Name name, RandomSettings settings)>> Handle(GetAllRandomSettingsQuery request,
            CancellationToken cancellationToken)
        {
            return await _matchRepository.GetAllRandomSettings(cancellationToken);
        }
    }
}
