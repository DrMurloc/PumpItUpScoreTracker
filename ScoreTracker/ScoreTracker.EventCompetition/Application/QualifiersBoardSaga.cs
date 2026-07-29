using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
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
        IDateTimeOffsetAccessor dateTimeOffset,
        IScoreReader scores,
        IChartRepository charts)
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

            // A signed-in player with no entry yet is exactly who suggestions are for, so they
            // get a transient empty one rather than nothing. Nothing here is persisted.
            var mine = currentUser.IsLoggedIn
                ? all.FirstOrDefault(q => q.UserId == currentUser.User.Id)
                  ?? new UserQualifiers(config, currentUser.User.Name, currentUser.User.Id,
                      new Dictionary<Guid, UserQualifiers.Submission>())
                : null;

            return new QualifierBoard(
                config,
                tournament?.Name ?? Name.From("Qualifiers"),
                ranked,
                withoutScores,
                StandingFor(scored, withoutScores.Length, cancellationToken),
                autoSubmit,
                config.CutoffTime != null && config.CutoffTime < dateTimeOffset.Now,
                await SuggestFor(mine, config, cancellationToken));
        }

        /// <summary>
        ///     Which charts to nudge the player toward next. Two sources, both from the original
        ///     page: the chart one rung above anything they have already SSS'd, and the pool
        ///     charts their existing scores say suit them best. A chart they have already posted
        ///     is never a suggestion.
        /// </summary>
        private async Task<IReadOnlyList<Guid>> SuggestFor(UserQualifiers? mine, QualifiersConfiguration config,
            CancellationToken cancellationToken)
        {
            if (mine == null) return Array.Empty<Guid>();

            var suggested = new HashSet<Guid>();

            if (!config.AllCharts)
            {
                // Cleared this one outright — the next rung up is the obvious ask. The ladder is
                // the pool's own order: level, then type.
                var ladder = config.Charts.OrderBy(c => (int)c.Level).ThenBy(c => c.Type).ToArray();
                var sssFloor = PhoenixLetterGrade.SSS.GetMinimumScoreFor(config.Mix);
                foreach (var submission in mine.Submissions.Values.Where(s => s.Score >= sssFloor))
                {
                    var index = Array.FindIndex(ladder, c => c.Id == submission.ChartId);
                    if (index >= 0 && index < ladder.Length - 1) suggested.Add(ladder[index + 1].Id);
                }
            }

            if (currentUser.IsLoggedIn)
            {
                var best = (await scores.GetBestScores(config.Mix, currentUser.User.Id, cancellationToken))
                    .ToDictionary(s => s.ChartId);
                var catalog = (await charts.GetCharts(config.Mix, cancellationToken: cancellationToken))
                    .ToDictionary(c => c.Id);

                // A folder average stands in for charts they have never played, but only once
                // there are enough plays in it to mean anything.
                var levelAverages = best.Values
                    .Where(s => s.Score != null && catalog.ContainsKey(s.ChartId))
                    .GroupBy(s => (catalog[s.ChartId].Level, catalog[s.ChartId].Type))
                    .Where(g => g.Count() >= 10)
                    .ToDictionary(g => g.Key, g => (int)g.Average(s => (int)s.Score!.Value));

                int Predicted(Chart chart) =>
                    best.TryGetValue(chart.Id, out var score) && score.Score != null
                        ? (int)score.Score.Value
                        : levelAverages.TryGetValue((chart.Level, chart.Type), out var average)
                            ? average
                            : 0;

                var floor = PhoenixLetterGrade.AA.GetMinimumScoreFor(config.Mix);
                foreach (var chart in config.Charts
                             .Where(c => Predicted(c) > floor)
                             .OrderByDescending(c => mine.Rating(c.Level, Predicted(c)))
                             .Take(config.PlayCount))
                    suggested.Add(chart.Id);
            }

            foreach (var chartId in mine.Submissions.Keys) suggested.Remove(chartId);
            return suggested.ToArray();
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
