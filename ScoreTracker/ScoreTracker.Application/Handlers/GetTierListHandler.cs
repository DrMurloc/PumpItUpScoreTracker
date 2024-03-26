using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class GetTierListHandler : IRequestHandler<GetTierListQuery, IEnumerable<SongTierListEntry>>
    {
        private readonly ITierListRepository _tierLists;

        public GetTierListHandler(ITierListRepository tierLists)
        {
            _tierLists = tierLists;
        }

        public async Task<IEnumerable<SongTierListEntry>> Handle(GetTierListQuery request,
            CancellationToken cancellationToken)
        {
            return await _tierLists.GetAllEntries(request.TierListName, cancellationToken);
        }
    }
}
