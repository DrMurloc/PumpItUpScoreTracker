using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.EventCompetition.Application
{
    internal sealed class QualifiersSaga(IQualifiersRepository qualifiersRepo, IUserReader userRepo,
        ILogger<QualifiersSaga> logger, IMediator mediator) : IConsumer<ScoreImportCompletedEvent>
    {
        public async Task Consume(ConsumeContext<ScoreImportCompletedEvent> context)
        {
            var charts = context.Message.Scores.ToDictionary(e => e.ChartId);
            var tournaments =
                await qualifiersRepo.GetRegisteredTournaments(context.Message.UserId, context.CancellationToken);
            foreach (var tournament in tournaments)
            {
                var qualifiers = await qualifiersRepo.GetQualifiersConfiguration(tournament, context.CancellationToken);
                var userEntry = await qualifiersRepo.GetQualifiers(tournament, context.Message.UserId, qualifiers,
                    context.CancellationToken);
             
                if (userEntry == null)
                {
                    var userInfo = await userRepo.GetUser(context.Message.UserId, context.CancellationToken);
                    if (userInfo == null)
                    {
                        logger.LogError("Couldn't find user " + context.Message.UserId +
                                        " for auto-qualifier registration");
                        continue;
                    }

                    userEntry = new UserQualifiers(qualifiers, false, userInfo.Name, userInfo.Id,
                        new Dictionary<Guid, UserQualifiers.Submission>());
                }

                var needsSaved = false;
                foreach (var matchedChart in qualifiers.Charts.Where(c => charts.ContainsKey(c.Id)))
                {
                    var existing = userEntry.Submissions.TryGetValue(matchedChart.Id, out var r) ? r?.Score : null;
                    var score = charts[matchedChart.Id].Score;
                    if (existing >= score) continue;

                    userEntry.AddPhoenixScore(matchedChart.Id, score, null);
                    needsSaved = true;
                }

                if (needsSaved) await mediator.Send(new SaveQualifiersCommand(tournament, userEntry));
            }
        }
    }
}
