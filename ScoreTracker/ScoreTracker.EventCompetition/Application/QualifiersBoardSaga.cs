using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.EventCompetition.Infrastructure;

namespace ScoreTracker.EventCompetition.Application
{
    /// <summary>
    ///     Everything the player-facing qualifiers page dispatches. The page owns no data access
    ///     of its own, so photo URLs simply never leave this class.
    /// </summary>
    internal sealed class QualifiersBoardSaga(
        IQualifiersRepository qualifiers,
        IMediator mediator,
        ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTimeOffset)
        : IRequestHandler<GetQualifiersBoardQuery, QualifierBoard>,
            IRequestHandler<SubmitQualifierScoreCommand>,
            IRequestHandler<SetQualifierAutoSubmitCommand>,
            IRequestHandler<TournamentHasQualifiersQuery, bool>
    {
        public async Task<QualifierBoard> Handle(GetQualifiersBoardQuery request,
            CancellationToken cancellationToken)
        {
            var config = await qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
            var all = (await qualifiers.GetAllUserQualifiers(request.TournamentId, config, cancellationToken))
                .ToArray();

            var scored = all.Where(q => q.Submissions.Any())
                .OrderByDescending(q => q.CalculateScore())
                .ToArray();
            var withoutScores = all.Where(q => !q.Submissions.Any()).Select(q => q.UserName).ToArray();

            var ranked = scored.Select(q => new QualifierEntry(
                q.UserName,
                q.UserId != null,
                q.CalculateScore(),
                q.BestCharts().Select(c => new QualifierPlay(
                    c.Chart, c.Score, c.Rating, q.Submissions[c.Chart.Id].Source)).ToArray())).ToArray();

            var tournament = (await mediator.Send(new GetAllTournamentsQuery(), cancellationToken))
                .FirstOrDefault(t => t.Id == request.TournamentId);

            var autoSubmit = currentUser.IsLoggedIn &&
                             (await qualifiers.GetRegisteredUsers(request.TournamentId, cancellationToken))
                             .Contains(currentUser.User.Id);

            return new QualifierBoard(
                config,
                tournament?.Name ?? Name.From("Qualifiers"),
                ranked,
                withoutScores,
                StandingFor(scored, withoutScores.Length, cancellationToken),
                autoSubmit,
                config.CutoffTime != null && config.CutoffTime < dateTimeOffset.Now);
        }

        private QualifierStanding? StandingFor(IReadOnlyList<UserQualifiers> scored, int withoutScores,
            CancellationToken cancellationToken)
        {
            if (!currentUser.IsLoggedIn) return null;

            var index = -1;
            for (var i = 0; i < scored.Count; i++)
                if (scored[i].UserId == currentUser.User.Id)
                    index = i;
            if (index == -1) return null;

            var mine = scored[index];
            var ahead = index > 0 ? scored[index - 1] : null;
            return new QualifierStanding(
                mine.UserName,
                index + 1,
                scored.Count + withoutScores,
                mine.CalculateScore(),
                ahead == null ? null : ahead.CalculateScore() - mine.CalculateScore(),
                ahead?.UserName);
        }

        public async Task Handle(SubmitQualifierScoreCommand request, CancellationToken cancellationToken)
        {
            var config = await qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
            if (config.CutoffTime != null && config.CutoffTime < dateTimeOffset.Now)
                throw new QualifiersClosedException();

            var entry = await qualifiers.GetQualifiers(request.TournamentId, request.UserName, config,
                            cancellationToken)
                        ?? new UserQualifiers(config, request.UserName,
                            currentUser.IsLoggedIn ? currentUser.User.Id : null,
                            new Dictionary<Guid, UserQualifiers.Submission>());

            // An entry that belongs to an account can only be edited by that account. There is no
            // name to claim and nothing to reconcile: if a player ends up with two entries, an
            // organiser drops one.
            if (entry.UserId != null && (!currentUser.IsLoggedIn || entry.UserId != currentUser.User.Id))
                throw new NotAuthorizedException("edit another player's qualifier entry");

            if (request.Judgements is { } j)
                entry.AddXXScore(request.ChartId, j.Perfects, j.Greats, j.Goods, j.Bads, j.Misses, j.MaxCombo,
                    request.PhotoUrl, dateTimeOffset.Now);
            else
                entry.AddManualScore(request.ChartId, request.Score ?? 0, request.PhotoUrl, dateTimeOffset.Now);

            await mediator.Send(new SaveQualifiersCommand(request.TournamentId, entry), cancellationToken);
        }

        public async Task<bool> Handle(TournamentHasQualifiersQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var config = await qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
                return config.PlayCount > 0;
            }
            catch (Exception)
            {
                // A tournament with no qualifiers configuration simply has none.
                return false;
            }
        }

        public async Task Handle(SetQualifierAutoSubmitCommand request, CancellationToken cancellationToken)
        {
            if (!currentUser.IsLoggedIn) throw new UserNotLoggedInException();

            if (request.Enabled)
                await qualifiers.RegisterUserToTournament(request.TournamentId, currentUser.User.Id,
                    cancellationToken);
            else
                await qualifiers.UnregisterUserFromTournament(request.TournamentId, currentUser.User.Id,
                    cancellationToken);
        }
    }
}
