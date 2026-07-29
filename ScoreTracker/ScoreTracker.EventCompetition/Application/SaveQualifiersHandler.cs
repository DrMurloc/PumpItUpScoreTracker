using MediatR;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.EventCompetition.Infrastructure;

namespace ScoreTracker.EventCompetition.Application
{
    internal sealed class SaveQualifiersHandler : IRequestHandler<SaveQualifiersCommand>
    {
        private readonly IQualifiersRepository _qualifiers;
        private readonly IBotClient _botClient;

        public SaveQualifiersHandler(IQualifiersRepository qualifiers, IBotClient botClient)
        {
            _botClient = botClient;
            _qualifiers = qualifiers;
        }

        public async Task Handle(SaveQualifiersCommand request, CancellationToken cancellationToken)
        {
            // One read, not two: the board before the save plus the entry being saved is the
            // board after it, and the second round trip was only ever recomputing that.
            var previousLeaderboard =
                (await _qualifiers.GetAllUserQualifiers(request.TournamentId, request.Qualifiers.Configuration,
                    cancellationToken)).ToArray();
            await _qualifiers.SaveQualifiers(request.TournamentId, request.Qualifiers, cancellationToken);

            var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
            var user = request.Qualifiers.UserName;
            var orderedOldLeaderboard = previousLeaderboard.Where(q => q.CalculateScore() > .001)
                .OrderByDescending(q => q.CalculateScore())
                .Select((q, i) => (q, i + 1)).ToArray();

            var newLeaderboard = previousLeaderboard.Where(q => q.UserName != user)
                .Append(request.Qualifiers);

            var orderedNewLeaderboard = newLeaderboard.Where(q => q.CalculateScore() > .001)
                .OrderByDescending(q => q.CalculateScore())
                .Select((q, i) => (q, i + 1)).ToArray();

            // A save that leaves the entry at zero - an empty submission, or a scoring type that
            // returns none - is filtered out of the ordered board above. Announcing a placement
            // for it is meaningless, and looking one up used to throw.
            var placed = orderedNewLeaderboard.FirstOrDefault(kv => kv.q.UserName == user);
            if (placed.q == null) return;

            var newPlace = placed.Item2;
            if (orderedOldLeaderboard.All(o => o.q.UserName != user))
            {
                await _botClient.SendMessage(
                    $"A new challenger approaches! Welcome {user} to the [Qualifier Leaderboard](https://piuscores.arroweclip.se/Tournament/{request.TournamentId}/Qualifiers)!",
                    config.NotificationChannel, cancellationToken);
                /*
                if (newPlace > 22 || orderedNewLeaderboard.Length < 23)
                {
                    var place23 = orderedNewLeaderboard[22].q;
                    await _botClient.SendMessage($"{place23.UserName} has been knocked out of Pros!",
                        config.NotificationChannel,
                        cancellationToken);
                }*/
            }
            else if (request.Qualifiers.Submissions.Count >= request.Qualifiers.Configuration.PlayCount)
            {
                var oldPlace = orderedOldLeaderboard.First(kv => kv.q.UserName == user).Item2;
                if (oldPlace != newPlace)
                {
                    var message =
                        $"{user} has progressed to {newPlace} on the [Leaderboard](https://piuscores.arroweclip.se/Tournament/{request.TournamentId}/Qualifiers)!";
                    foreach (var best in request.Qualifiers.BestCharts())
                        message += $@"
- {best.Chart.Song.Name} #DIFFICULTY|{best.Chart.DifficultyString}# - {best.Score} #LETTERGRADE|{best.Score.LetterGradeFor(best.Chart.Mix)}# ({best.Rating:0.##} rating)";

                    await _botClient.SendMessage(
                        message,
                        config.NotificationChannel,
                        cancellationToken);
                }
                /*
                if (newPlace > 22 || oldPlace <= 22 || orderedNewLeaderboard.Length < 23)
                {
                    var place23 = orderedNewLeaderboard[22].q;
                    await _botClient.SendMessage($"{place23.UserName} has been knocked out of Pros!",
                        config.NotificationChannel,
                        cancellationToken);
                }*/
            }
        }
    }
}
