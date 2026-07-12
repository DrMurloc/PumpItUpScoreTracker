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
    ///     role-carrying invite links and removes staff (never themselves — a tournament
    ///     keeps at least the Head TO who's doing the managing); redeeming an invite grants
    ///     the role without ever downgrading an existing one. Listing (IsUnlisted = false)
    ///     stays owner-curated.
    /// </summary>
    internal sealed class TournamentRoleSaga :
        IRequestHandler<CreateUnlistedTournamentCommand>,
        IRequestHandler<CreateTournamentRoleInviteCommand, Guid>,
        IRequestHandler<RedeemTournamentRoleInviteCommand, Guid>,
        IRequestHandler<GetTournamentRoleInvitesQuery, IEnumerable<TournamentRoleInviteRecord>>,
        IRequestHandler<DeleteTournamentRoleInviteCommand>,
        IRequestHandler<RemoveTournamentRoleCommand>,
        IRequestHandler<SetTournamentDiscordChannelCommand>,
        IRequestHandler<GetTournamentDiscordChannelQuery, ulong?>
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

        public async Task<IEnumerable<TournamentRoleInviteRecord>> Handle(GetTournamentRoleInvitesQuery request,
            CancellationToken cancellationToken)
        {
            await EnsureHeadOrganizer(request.TournamentId, "view this tournament's invites", cancellationToken);
            return await _tournaments.GetRoleInvites(request.TournamentId, cancellationToken);
        }

        public async Task Handle(DeleteTournamentRoleInviteCommand request, CancellationToken cancellationToken)
        {
            await EnsureHeadOrganizer(request.TournamentId, "revoke this tournament's invites", cancellationToken);
            var invite = await _tournaments.GetRoleInvite(request.Token, cancellationToken);
            // Tokens are global; only delete when it actually belongs to the tournament the
            // caller is authorized on.
            if (invite == null || invite.TournamentId != request.TournamentId) return;

            await _tournaments.DeleteRoleInvite(request.Token, cancellationToken);
        }

        public async Task Handle(RemoveTournamentRoleCommand request, CancellationToken cancellationToken)
        {
            await EnsureHeadOrganizer(request.TournamentId, "remove this tournament's staff", cancellationToken);
            // Removing yourself would orphan the tournament (or at least strand this
            // drawer mid-session) — hand the Head TO role off first.
            if (_currentUser.User.Id == request.UserId)
                throw new NotAuthorizedException("remove yourself from your own tournament");

            await _tournaments.RevokeRole(request.TournamentId, request.UserId, cancellationToken);
        }

        public async Task Handle(SetTournamentDiscordChannelCommand request, CancellationToken cancellationToken)
        {
            await EnsureHeadOrganizer(request.TournamentId, "configure this tournament's Discord channel",
                cancellationToken);
            await _tournaments.SetDiscordChannel(request.TournamentId, request.ChannelId, cancellationToken);
        }

        public async Task<ulong?> Handle(GetTournamentDiscordChannelQuery request, CancellationToken cancellationToken)
        {
            // Any staff role: assistants need it to see the push button. Never anonymous —
            // a channel id is server-internal.
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();
            if (!_currentUser.IsLoggedInAsAdmin)
            {
                var roles = await _mediator.Send(new GetTournamentRolesQuery(request.TournamentId), cancellationToken);
                if (roles.All(r => r.UserId != _currentUser.User.Id))
                    throw new NotAuthorizedException("view this tournament's Discord channel");
            }

            return await _tournaments.GetDiscordChannel(request.TournamentId, cancellationToken);
        }

        private async Task EnsureHeadOrganizer(Guid tournamentId, string action, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();
            if (_currentUser.IsLoggedInAsAdmin) return;

            var roles = await _mediator.Send(new GetTournamentRolesQuery(tournamentId), cancellationToken);
            if (!roles.Any(r => r.UserId == _currentUser.User.Id && r.Role == TournamentRole.HeadTournamentOrganizer))
                throw new NotAuthorizedException(action);
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
