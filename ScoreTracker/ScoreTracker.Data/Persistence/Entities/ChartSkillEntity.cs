﻿using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId))]
    [Index(nameof(SkillName))]
    public sealed class ChartSkillEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid ChartId { get; set; }
        [Required] [MaxLength(64)] public string SkillName { get; set; } = string.Empty;
        public bool IsHighlighted { get; set; }
    }
}
