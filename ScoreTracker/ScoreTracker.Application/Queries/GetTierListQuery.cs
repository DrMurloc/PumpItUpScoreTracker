using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetTierListQuery(Name TierListName) : IRequest<IEnumerable<SongTierListEntry>>
    {
    }
}
