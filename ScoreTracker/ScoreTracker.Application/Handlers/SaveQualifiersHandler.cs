using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class SaveQualifiersHandler : IRequestHandler<SaveQualifiersCommand>
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
            var previousLeaderboard =
                await _qualifiers.GetAllUserQualifiers(request.TournamentId, request.Qualifiers.Configuration,
                    cancellationToken);
            await _qualifiers.SaveQualifiers(request.TournamentId, request.Qualifiers, cancellationToken);
            var newLeaderboard =
                await _qualifiers.GetAllUserQualifiers(request.TournamentId, request.Qualifiers.Configuration,
                    cancellationToken);
            var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
            var user = request.Qualifiers.UserName;
            var orderedOldLeaderboard = previousLeaderboard.OrderByDescending(q => q.CalculateScore())
                .Select((q, i) => (q, i + 1)).ToArray();

            var orderedNewLeaderboard = newLeaderboard.OrderByDescending(q => q.CalculateScore())
                .Select((q, i) => (q, i + 1)).ToArray();

            var newPlace = orderedNewLeaderboard.First(kv => kv.q.UserName == user).Item2;
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
- {best.Chart.Song.Name} #DIFFICULTY|{best.Chart.DifficultyString}# - {best.Score} #LETTERGRADE|{best.Score.LetterGrade}# ({best.Rating} rating)";

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
