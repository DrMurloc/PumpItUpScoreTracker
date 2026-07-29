using System.Text.RegularExpressions;
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
    ///     The organiser surface: the only place photos are read, and the only place an entry can
    ///     be deleted. Every entry point re-checks the caller's tournament role, because the UI
    ///     hiding a button is not an authorization boundary.
    /// </summary>
    internal sealed class QualifiersAdminSaga(
        IQualifiersRepository qualifiers,
        IMediator mediator,
        ICurrentUserAccessor currentUser)
        : IRequestHandler<GetQualifiersAdminQuery, QualifierAdminView>,
            IRequestHandler<DeleteQualifierEntryCommand>,
            IRequestHandler<DeleteQualifierSubmissionCommand>
    {
        private static readonly Regex NonAlphanumeric = new("[^a-z0-9]", RegexOptions.Compiled);

        private async Task RequireOrganizer(Guid tournamentId, CancellationToken cancellationToken)
        {
            if (!currentUser.IsLoggedIn) throw new UserNotLoggedInException();
            if (currentUser.IsLoggedInAsAdmin) return;

            var roles = await mediator.Send(new GetTournamentRolesQuery(tournamentId), cancellationToken);
            var mine = roles.FirstOrDefault(r => r.UserId == currentUser.User.Id);
            if (mine?.Role is not (TournamentRole.HeadTournamentOrganizer or TournamentRole.TournamentOrganizer))
                throw new NotAuthorizedException("manage this tournament's qualifiers");
        }

        public async Task<QualifierAdminView> Handle(GetQualifiersAdminQuery request,
            CancellationToken cancellationToken)
        {
            await RequireOrganizer(request.TournamentId, cancellationToken);

            var config = await qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
            var all = (await qualifiers.GetAllUserQualifiers(request.TournamentId, config, cancellationToken))
                .ToArray();
            var firstSeen = await qualifiers.GetFirstSubmissionDates(request.TournamentId, cancellationToken);

            var entries = all.Where(q => q.Submissions.Any())
                .OrderByDescending(q => q.CalculateScore())
                .Select(q => new QualifierAdminEntry(
                    q.UserName,
                    q.UserId != null,
                    q.CalculateScore(),
                    FirstSeenFor(q.UserName, q.Submissions.Values, firstSeen),
                    // Every submission, not just the counting ones — an organiser reviewing a
                    // photo needs the play they are looking at, wherever it placed.
                    q.Submissions.Values
                        .Where(s => config.Charts.Any(c => c.Id == s.ChartId))
                        .Select(s => new QualifierAdminPlay(
                            config.Charts.First(c => c.Id == s.ChartId),
                            s.Score,
                            q.Rating(s.ChartId),
                            s.Source,
                            s.PhotoUrl,
                            s.SubmittedAt))
                        .OrderByDescending(p => p.Rating)
                        .ToArray()))
                .ToArray();

            var tournament = (await mediator.Send(new GetAllTournamentsQuery(), cancellationToken))
                .FirstOrDefault(t => t.Id == request.TournamentId);

            return new QualifierAdminView(
                config,
                tournament?.Name ?? Name.From("Qualifiers"),
                entries,
                all.Where(q => !q.Submissions.Any()).Select(q => q.UserName).ToArray(),
                FindDuplicates(entries));
        }

        private static DateTimeOffset FirstSeenFor(Name userName,
            IEnumerable<UserQualifiers.Submission> submissions,
            IDictionary<string, DateTimeOffset> history)
        {
            if (history.TryGetValue(userName.ToString(), out var recorded)) return recorded;

            // No history row (or a tournament that predates them): fall back to the earliest
            // submission that carries a timestamp of its own.
            var stamped = submissions.Where(s => s.SubmittedAt != default).ToArray();
            return stamped.Length == 0 ? default : stamped.Min(s => s.SubmittedAt);
        }

        /// <summary>
        ///     Names that fold together, where at least one side is signed in and at least one is
        ///     not. That asymmetry is what makes it worth flagging: it is the trail a player
        ///     leaves by submitting anonymously and then coming back with an account.
        /// </summary>
        private static IReadOnlyList<QualifierDuplicateGroup> FindDuplicates(
            IReadOnlyList<QualifierAdminEntry> entries)
        {
            return entries
                .GroupBy(e => Normalize(e.UserName))
                .Where(g => g.Count() > 1 && g.Any(e => e.HasAccount) && g.Any(e => !e.HasAccount))
                .Select(g => new QualifierDuplicateGroup(
                    g.OrderByDescending(e => e.HasAccount).ThenBy(e => e.FirstSeen).ToArray()))
                .ToArray();
        }

        private static string Normalize(Name name)
        {
            var lowered = name.ToString().ToLowerInvariant();
            var stripped = NonAlphanumeric.Replace(lowered, string.Empty);
            // A trailing run of digits is usually a tag suffix rather than a different player.
            return stripped.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        }

        public async Task Handle(DeleteQualifierEntryCommand request, CancellationToken cancellationToken)
        {
            await RequireOrganizer(request.TournamentId, cancellationToken);
            await qualifiers.DeleteQualifiers(request.TournamentId, request.UserName, cancellationToken);
        }

        public async Task Handle(DeleteQualifierSubmissionCommand request, CancellationToken cancellationToken)
        {
            await RequireOrganizer(request.TournamentId, cancellationToken);

            var config = await qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
            var entry = await qualifiers.GetQualifiers(request.TournamentId, request.UserName, config,
                cancellationToken);
            if (entry == null || !entry.Submissions.Remove(request.ChartId)) return;

            // Saved through the repository rather than the command: dropping a score should not
            // announce a placement change to Discord.
            await qualifiers.SaveQualifiers(request.TournamentId, entry, cancellationToken);
        }
    }
}
