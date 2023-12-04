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
        IRequestHandler<GetAllRandomSettingsQuery, IEnumerable<(Name name, RandomSettings settings)>>,
        IRequestHandler<GetMatchLinksQuery, IEnumerable<MatchLink>>,
        IRequestHandler<GetMatchLinksFromMatchQuery, IEnumerable<MatchLink>>,
        IRequestHandler<CreateMatchLinkCommand>,
        IRequestHandler<DeleteMatchLinkCommand>

    {
        private readonly IMatchRepository _matchRepository;
        private readonly IMediator _mediator;
        private readonly IAdminNotificationClient _admins;

        public MatchSaga(IMediator mediator, IMatchRepository matchRepository, IAdminNotificationClient admins)
        {
            _matchRepository = matchRepository;
            _mediator = mediator;
            _admins = admins;
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
                    var points = scoreGroup.Key == 0 ? 0 : scoring[pointIndex];
                    foreach (var player in scoreGroup) match.Points[player][chartIndex] = points;

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
            var links = await _matchRepository.GetMatchLinksByFromMatchName(match.MatchName, cancellationToken);
            foreach (var link in links)
            {
                var nextMatch = await _matchRepository.GetMatch(link.ToMatch, cancellationToken);
                var progressers = link.IsWinners
                    ? newMatchState.FinalPlaces.Take(link.PlayerCount)
                    : newMatchState.FinalPlaces.Reverse().Take(link.PlayerCount);
                foreach (var player in progressers)
                {
                    if (nextMatch.Players.Contains(player)) continue;
                    var nextIndex = nextMatch.Players.Select((p, i) => (p, i))
                        .Where(p => p.p.ToString().StartsWith("Unknown ", StringComparison.OrdinalIgnoreCase))
                        .Select(p => (int?)p.i)
                        .FirstOrDefault();
                    if (nextIndex == null)
                    {
                        await _admins.NotifyAdmin(
                            $"Player {player} Couldn't progress from {newMatchState.MatchName} to {nextMatch.MatchName}",
                            cancellationToken);
                        continue;
                    }

                    nextMatch.Players[nextIndex.Value] = player;
                }

                await _matchRepository.SaveMatch(nextMatch, cancellationToken);
                await _mediator.Publish(new MatchUpdatedEvent(nextMatch), cancellationToken);
            }

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

        public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksQuery request,
            CancellationToken cancellationToken)
        {
            return await _matchRepository.GetAllMatchLinks(cancellationToken);
        }

        public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksFromMatchQuery request,
            CancellationToken cancellationToken)
        {
            return await _matchRepository.GetMatchLinksByFromMatchName(request.FromMatchName, cancellationToken);
        }

        public async Task<Unit> Handle(CreateMatchLinkCommand request, CancellationToken cancellationToken)
        {
            await _matchRepository.SaveMatchLink(request.MatchLink, cancellationToken);
            return Unit.Value;
        }

        public async Task<Unit> Handle(DeleteMatchLinkCommand request, CancellationToken cancellationToken)
        {
            await _matchRepository.DeleteMatchLink(request.FromName, request.ToName, cancellationToken);
            return Unit.Value;
        }
    }
}
