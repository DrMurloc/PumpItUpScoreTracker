using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Persistence
{
    public sealed class UserQualifierEntity
    {
        [Key] public Guid Id { get; set; }

        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Entries { get; set; } = string.Empty;

        public bool IsApproved { get; set; }
    }
}
