using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class TournamentHandler : IRequestHandler<SaveTournamentCommand>,
        IRequestHandler<GetAllTournamentsQuery, IEnumerable<TournamentRecord>>,
        IRequestHandler<GetTournamentQuery, TournamentConfiguration>
    {
        private readonly ITournamentRepository _tournaments;

        public TournamentHandler(ITournamentRepository tournaments)
        {
            _tournaments = tournaments;
        }

        public async Task Handle(SaveTournamentCommand request, CancellationToken cancellationToken)
        {
            await _tournaments.CreateOrSaveTournament(request.Tournament, cancellationToken);
        }

        public async Task<IEnumerable<TournamentRecord>> Handle(GetAllTournamentsQuery request,
            CancellationToken cancellationToken)
        {
            return await _tournaments.GetAllTournaments(cancellationToken);
        }

        public async Task<TournamentConfiguration> Handle(GetTournamentQuery request,
            CancellationToken cancellationToken)
        {
            var result = await _tournaments.GetTournament(request.TournamentId, cancellationToken);
            return result;
        }
    }
}