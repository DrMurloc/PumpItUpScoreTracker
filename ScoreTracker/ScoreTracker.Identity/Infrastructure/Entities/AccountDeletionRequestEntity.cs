using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Identity.Infrastructure.Entities;

// A player asked for their account to go. Deliberately not MergeRequest: that record demands a
// SurvivorUserId and carries the logins a merge moved, both meaningless here, and making the
// survivor nullable would force every merge query to defend against a row that is not a merge.
[Index(nameof(UserId))]
internal sealed class AccountDeletionRequestEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public DateTimeOffset RequestedAt { get; set; }

    /// <summary>When the purge may begin. Until then the account works, it is just invisible.</summary>
    [Required]
    public DateTimeOffset PurgeAfter { get; set; }

    /// <summary>Null while pending; set when cancelled, which is what makes the row inert.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset? PurgedAt { get; set; }

    // What the account looked like before it was hidden, so cancelling restores it rather than
    // guessing. Same mechanism the merge undo uses.
    [Required] public bool WasPublic { get; set; }

    [MaxLength(100)] public string? GameTag { get; set; }
}
