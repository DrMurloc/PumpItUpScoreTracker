﻿using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class BestAttemptEntity
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public DateTimeOffset RecordedDate { get; set; }

    [Required] public string LetterGrade { get; set; } = string.Empty;
    [Required] public bool IsBroken { get; set; }
}