using System.ComponentModel.DataAnnotations;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class UserEntity
{
    [Key] public Guid Id { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required] public bool IsPublic { get; set; }
    public string? GameTag { get; set; }
    [Required] public string ProfileImage { get; set; }
    public string? CountryName { get; set; }

    /// <summary>
    ///     What the last piugame import saw, kept whether or not it is the avatar being shown.
    ///     Null until an import runs. Storing it separately is what makes "Back to Auto" instant
    ///     rather than a wait for the next import (docs/design/avatar-selection.md §1).
    /// </summary>
    [MaxLength(400)]
    public string? ImportedProfileImage { get; set; }

    /// <summary>The player chose their avatar, so imports must not overwrite it.</summary>
    [Required]
    public bool AvatarIsPinned { get; set; }
    [Required] public bool IsContentLocked { get; set; }
    [Required] public DateTimeOffset ClaimsInvalidatedAt { get; set; }

    /// <summary>
    ///     Deep scans of the official site this account may still run. A monthly job sets every row
    ///     back to the allowance, so the column holds a BALANCE rather than a usage count keyed to
    ///     a date — which also makes granting someone extra scans a single UPDATE that survives
    ///     until the next reset.
    ///     <para>
    ///         Initialised here rather than left to the column default: EF writes the property on
    ///         insert, so a new account would arrive with zero scans while every migrated one had
    ///         three.
    ///     </para>
    /// </summary>
    [Required]
    public int DeepScansRemaining { get; set; } = DeepScanAllowance.PerMonth;
}