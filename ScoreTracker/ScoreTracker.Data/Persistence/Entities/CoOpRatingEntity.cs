using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId))]
    public sealed class CoOpRatingEntity
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid ChartId { get; set; }
        [Required] public int Player { get; set; }
        [Required] public int Difficulty { get; set; }
    }
}