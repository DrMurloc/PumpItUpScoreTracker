using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class QualifierSubmissionDto
    {
        public Guid ChartId { get; set; }
        public int Score { get; set; }
        public string PhotoUrl { get; set; }
    }
}
