using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Application
{
    /// <summary>
    ///     Micro-tournament lifecycle (docs/design/randomizer-overhaul.md): any logged-in
    ///     user creates an unlisted tournament and becomes its Head TO; the Head TO mints
    ///     role-carrying invite links; redeeming one grants the role without ever
    ///     downgrading an existing one. Listing (IsUnlisted = false) stays owner-curated.
    /// </summary>
    internal sealed class TournamentRoleSaga :
        IRequestHandler<CreateUnlistedTournamentCommand>,
        IRequestHandler<CreateTournamentRoleInviteCommand, Guid>,
        IRequestHandler<RedeemTournamentRoleInviteCommand, Guid>
    {
        private readonly ITournamentRepository _tournaments;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IDateTimeOffsetAccessor _clock;
        private readonly IMediator _mediator;

        public TournamentRoleSaga(ITournamentRepository tournaments, ICurrentUserAccessor currentUser,
            IDateTimeOffsetAccessor clock, IMediator mediator)
        {
            _tournaments = tournaments;
            _currentUser = currentUser;
            _clock = clock;
            _mediator = mediator;
        }

        public async Task Handle(CreateUnlistedTournamentCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();

            await _tournaments.CreateUnlistedTournament(new TournamentRecord(request.TournamentId, request.Name, 0,
                TournamentType.Stamina, "Remote", false, null, null, null, false), cancellationToken);
            await _tournaments.SetRole(request.TournamentId, _currentUser.User.Id,
                TournamentRole.HeadTournamentOrganizer, cancellationToken);
        }

        public async Task<Guid> Handle(CreateTournamentRoleInviteCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();

            var roles = await _mediator.Send(new GetTournamentRolesQuery(request.TournamentId), cancellationToken);
            var isHead = roles.Any(r =>
                r.UserId == _currentUser.User.Id && r.Role == TournamentRole.HeadTournamentOrganizer);
            if (!isHead && !_currentUser.IsLoggedInAsAdmin)
                throw new NotAuthorizedException("create invites for this tournament");

            return await _tournaments.CreateRoleInvite(request.TournamentId, request.Role, request.ExpiresAt,
                _currentUser.User.Id, cancellationToken);
        }

        public async Task<Guid> Handle(RedeemTournamentRoleInviteCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();

            var invite = await _tournaments.GetRoleInvite(request.Token, cancellationToken)
                         ?? throw new TournamentInviteInvalidException();
            if (invite.ExpiresAt != null && invite.ExpiresAt < _clock.Now)
                throw new TournamentInviteInvalidException();

            var roles = await _mediator.Send(new GetTournamentRolesQuery(invite.TournamentId), cancellationToken);
            if (roles.All(r => r.UserId != _currentUser.User.Id))
                await _tournaments.SetRole(invite.TournamentId, _currentUser.User.Id, invite.Role, cancellationToken);

            return invite.TournamentId;
        }
    }
}
