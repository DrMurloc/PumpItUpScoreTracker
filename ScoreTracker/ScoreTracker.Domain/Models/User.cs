using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Models;

/// <summary>
///     <see cref="ProfileImage" /> is always <b>the avatar to show</b>, whether it arrived from an
///     import or the player chose it. Everything that renders an avatar reads that one property and
///     needs to know nothing about where it came from.
///     <para>
///         The other two say where it came from. <see cref="AvatarIsPinned" /> means the player
///         picked it and an import must leave it alone;
///         <see cref="ImportedProfileImage" /> is what the last import saw <i>regardless</i>, which
///         is what lets "Back to Auto" restore the real piugame picture immediately instead of
///         leaving the player waiting for their next import (docs/design/avatar-selection.md §1).
///     </para>
/// </summary>
public sealed record User(
    Guid Id,
    Name Name,
    bool IsPublic,
    Name? GameTag,
    Uri ProfileImage,
    Name? Country,
    bool IsContentLocked = false,
    DateTimeOffset ClaimsInvalidatedAt = default,
    Uri? ImportedProfileImage = null,
    bool AvatarIsPinned = false)
{
    private static readonly Guid DrMurlocGuid = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");

    public bool IsAdmin => Id == DrMurlocGuid;
}