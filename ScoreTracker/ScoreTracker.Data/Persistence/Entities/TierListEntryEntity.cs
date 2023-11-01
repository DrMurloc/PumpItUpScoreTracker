using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TierListName))]
    public class TierListEntryEntity
    {
        [Key] public Guid Id { get; set; }
        public string TierListName { get; set; } = string.Empty;
        public Guid ChartId { get; set; }
        public string Category { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}
