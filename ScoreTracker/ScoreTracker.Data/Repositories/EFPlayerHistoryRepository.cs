using MediatR;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Application.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFPlayerHistoryRepository(ChartAttemptDbContext database) : IPlayerHistoryRepository,
        IRequestHandler<GetPlayerHistoryQuery, IEnumerable<PlayerRatingRecord>>
    {
        public async Task WriteHistory(PlayerRatingRecord record, CancellationToken cancellationToken)
        {
            await database.PlayerHistory.AddAsync(new PlayerHistoryEntity
            {
                UserId = record.UserId,
                CoOpRating = record.CoOpRating,
                Date = record.Date,
                CompetitiveLevel = record.CompetitiveLevel,
                SinglesLevel = record.SinglesLevel,
                DoublesLevel = record.DoublesLevel,
                PassCount = record.PassCount
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }


        public async Task<IEnumerable<PlayerRatingRecord>> Handle(GetPlayerHistoryQuery request,
            CancellationToken cancellationToken)
        {
            return await database.PlayerHistory.Where(r => r.UserId == request.UserId)
                .Select(r => new PlayerRatingRecord(r.UserId, r.Date, r.CompetitiveLevel, r.SinglesLevel,
                    r.DoublesLevel, r.CoOpRating, r.PassCount)).ToArrayAsync(cancellationToken);
        }
    }
}
