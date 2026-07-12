using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.Randomizer.Contracts.Commands;
using ScoreTracker.Randomizer.Contracts.Events;
using ScoreTracker.Randomizer.Contracts.Queries;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Randomizer.Application
{
    /// <summary>
    ///     Draws + tournament settings (docs/design/randomizer-overhaul.md). Personal
    ///     context: any logged-in user. Tournament context: draw operations need any role
    ///     on the tournament (assistants run the tablet); match rename/delete and the
    ///     settings library need Head TO/TO (field-test round 8). Spectating
    ///     (GetDrawBySlugQuery) is unauthenticated and handled by the repository. Every
    ///     mutation publishes DrawUpdatedEvent in-process so staff devices and spectator
    ///     circuits re-render.
    /// </summary>
    internal sealed class DrawSaga :
        IRequestHandler<CreateDrawCommand, DrawDto>,
        IRequestHandler<RedrawCardsCommand, DrawDto>,
        IRequestHandler<RenameDrawCommand>,
        IRequestHandler<DeleteDrawCommand>,
        IRequestHandler<SetDrawCardStateCommand>,
        IRequestHandler<ClearVetoedCardsCommand, DrawDto>,
        IRequestHandler<AddChartToDrawCommand, DrawDto>,
        IRequestHandler<GetActiveDrawQuery, DrawDto?>,
        IRequestHandler<SaveTournamentRandomSettingsCommand>,
        IRequestHandler<DeleteTournamentRandomSettingsCommand>,
        IRequestHandler<CreateSettingsShareLinkCommand, Guid>
    {
        private readonly IDrawRepository _draws;
        private readonly IRandomizerRepository _settings;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IMediator _mediator;

        public DrawSaga(IDrawRepository draws, IRandomizerRepository settings, ICurrentUserAccessor currentUser,
            IMediator mediator)
        {
            _draws = draws;
            _settings = settings;
            _currentUser = currentUser;
            _mediator = mediator;
        }

        public async Task<DrawDto> Handle(CreateDrawCommand request, CancellationToken cancellationToken)
        {
            await EnsureCanOperateDraws(request.TournamentId, cancellationToken);
            DrawDto draw;
            if (request.TournamentId == null)
            {
                draw = await _draws.ReplacePersonalDraw(_currentUser.User.Id, request.Mix, request.ChartIds,
                    cancellationToken);
            }
            else
            {
                // A match is a named draw — the name is what the switcher, spectate tabs,
                // and verbal callouts hang on, so it's required (owner-locked round 6).
                if (string.IsNullOrWhiteSpace(request.MatchName))
                    throw new RandomizerException("Give the match a name.");

                draw = await _draws.CreateTournamentDraw(request.TournamentId.Value, request.MatchName.Trim(),
                    request.Mix, request.ChartIds, cancellationToken);
            }

            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
            return draw;
        }

        public async Task<DrawDto> Handle(RedrawCardsCommand request, CancellationToken cancellationToken)
        {
            var draw = await AuthorizeDraw(request.DrawId, cancellationToken);
            var result = await _draws.RedrawCards(request.DrawId, request.Mix, request.ChartIds, cancellationToken);
            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
            return result;
        }

        public async Task Handle(RenameDrawCommand request, CancellationToken cancellationToken)
        {
            var draw = await _draws.GetDraw(request.DrawId, cancellationToken)
                       ?? throw new RandomizerException("This draw no longer exists.");
            if (draw.TournamentId == null)
                throw new RandomizerException("Only tournament matches have names.");
            if (string.IsNullOrWhiteSpace(request.NewName))
                throw new RandomizerException("Give the match a name.");

            await EnsureOrganizer(draw.TournamentId.Value, "rename this tournament's matches", cancellationToken);
            await _draws.RenameDraw(request.DrawId, request.NewName.Trim(), cancellationToken);
            // Spectate tabs and other staff devices re-label live; the slug never moves.
            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
        }

        public async Task Handle(DeleteDrawCommand request, CancellationToken cancellationToken)
        {
            var draw = await AuthorizeDraw(request.DrawId, cancellationToken);
            // Deleting a match is organizer territory — assistants only run the tablet
            // (field-test round 8). Personal draws stay owner-scoped via AuthorizeDraw.
            if (draw.TournamentId != null)
                await EnsureOrganizer(draw.TournamentId.Value, "delete this tournament's matches", cancellationToken);
            await _draws.DeleteDraw(request.DrawId, cancellationToken);
            // Spectators following the deleted match refresh into its siblings.
            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
        }

        public async Task Handle(SetDrawCardStateCommand request, CancellationToken cancellationToken)
        {
            var draw = await AuthorizeDraw(request.DrawId, cancellationToken);
            await _draws.SetCardState(request.DrawId, request.PullId, request.State, cancellationToken);
            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
        }

        public async Task<DrawDto> Handle(ClearVetoedCardsCommand request, CancellationToken cancellationToken)
        {
            var draw = await AuthorizeDraw(request.DrawId, cancellationToken);
            var result = await _draws.ClearVetoed(request.DrawId, cancellationToken);
            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
            return result;
        }

        public async Task<DrawDto> Handle(AddChartToDrawCommand request, CancellationToken cancellationToken)
        {
            var draw = await AuthorizeDraw(request.DrawId, cancellationToken);
            var result = await _draws.AddChart(request.DrawId, request.ChartId, cancellationToken);
            await _mediator.Publish(new DrawUpdatedEvent(draw.Id, draw.Slug), cancellationToken);
            return result;
        }

        public async Task<DrawDto?> Handle(GetActiveDrawQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) return null;

            return await _draws.GetActiveDraw(request.TournamentId == null ? _currentUser.User.Id : null,
                request.TournamentId, cancellationToken);
        }

        public async Task Handle(SaveTournamentRandomSettingsCommand request, CancellationToken cancellationToken)
        {
            await EnsureCanManageSettings(request.TournamentId, cancellationToken);
            await _settings.SaveTournamentSettings(request.TournamentId, request.SettingsName, request.Settings,
                request.Mix, cancellationToken);
        }

        public async Task Handle(DeleteTournamentRandomSettingsCommand request, CancellationToken cancellationToken)
        {
            await EnsureCanManageSettings(request.TournamentId, cancellationToken);
            await _settings.DeleteTournamentSettings(request.TournamentId, request.SettingsName, cancellationToken);
        }

        public async Task<Guid> Handle(CreateSettingsShareLinkCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();

            return await _settings.EnsureShareToken(_currentUser.User.Id, request.SettingsName, cancellationToken);
        }

        private async Task<DrawDto> AuthorizeDraw(Guid drawId, CancellationToken cancellationToken)
        {
            var draw = await _draws.GetDraw(drawId, cancellationToken)
                       ?? throw new RandomizerException("This draw no longer exists.");
            await EnsureCanOperateDraws(draw.TournamentId, cancellationToken);
            return draw;
        }

        private async Task EnsureCanOperateDraws(Guid? tournamentId, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();
            if (tournamentId == null || _currentUser.IsLoggedInAsAdmin) return;

            var roles = await _mediator.Send(new GetTournamentRolesQuery(tournamentId.Value), cancellationToken);
            if (roles.All(r => r.UserId != _currentUser.User.Id))
                throw new NotAuthorizedException("operate this tournament's draws");
        }

        private Task EnsureCanManageSettings(Guid tournamentId, CancellationToken cancellationToken)
        {
            return EnsureOrganizer(tournamentId, "manage this tournament's randomizer settings", cancellationToken);
        }

        private async Task EnsureOrganizer(Guid tournamentId, string action, CancellationToken cancellationToken)
        {
            if (!_currentUser.IsLoggedIn) throw new UserNotLoggedInException();
            if (_currentUser.IsLoggedInAsAdmin) return;

            var roles = await _mediator.Send(new GetTournamentRolesQuery(tournamentId), cancellationToken);
            var allowed = roles.Any(r => r.UserId == _currentUser.User.Id &&
                                         r.Role is TournamentRole.HeadTournamentOrganizer
                                             or TournamentRole.TournamentOrganizer);
            if (!allowed) throw new NotAuthorizedException(action);
        }
    }
}
