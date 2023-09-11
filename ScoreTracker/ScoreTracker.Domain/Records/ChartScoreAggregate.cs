using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartScoreAggregate(Guid ChartId, int Count)
    {
    }
}