using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     What a player has agreed to, one row per player rather than per comment. A real row instead
///     of a UiSettings key because an agreement wants a timestamp and a version, and it should be
///     auditable if a dispute ever lands.
/// </summary>
[Index(nameof(UserId), IsUnique = true)]
internal sealed class CommentConsentEntity
{
    [Key] public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    ///     Set on the first public or community comment — never on a personal note, because the
    ///     rules are about how you treat other people and a note has none of those.
    /// </summary>
    public DateTimeOffset AgreedToTermsAt { get; set; }

    /// <summary>Versioned, so editing the rules re-prompts rather than silently reinterpreting a yes.</summary>
    public int TermsVersion { get; set; }

    /// <summary>
    ///     Set the first time a private-profile player posts publicly. Null for everyone else,
    ///     including private-profile players who only ever post to their own clubs — the consent is
    ///     collected when it becomes true, not in advance.
    /// </summary>
    public DateTimeOffset? ConsentedToPublicIdentityAt { get; set; }
}
