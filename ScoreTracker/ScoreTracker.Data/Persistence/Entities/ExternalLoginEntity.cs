using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class ExternalLoginEntity
{
    [Required] [MaxLength(32)] public string LoginProvider { get; set; }

    [Required] [MaxLength(64)] public string ExternalId { get; set; }

    [Required] public Guid UserId { get; set; }
}