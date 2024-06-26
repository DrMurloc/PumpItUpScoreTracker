﻿using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(UserId), nameof(ChartId), IsUnique = true)]
[Index(nameof(ChartId))]
public sealed class PhoenixRecordEntity
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public DateTimeOffset RecordedDate { get; set; }
    public int? Score { get; set; }
    public string? LetterGrade { get; set; } = string.Empty;
    public string? Plate { get; set; } = string.Empty;
    [Required] public bool IsBroken { get; set; }
}