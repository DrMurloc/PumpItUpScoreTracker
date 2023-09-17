using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class SessionEntryEntity
    {
        public Guid ChartId { get; set; } = Guid.Empty;
        public int Score { get; set; }
        public int SessionScore { get; set; }
        public string Plate { get; set; } = string.Empty;
        public bool IsBroken { get; set; }
    }
}