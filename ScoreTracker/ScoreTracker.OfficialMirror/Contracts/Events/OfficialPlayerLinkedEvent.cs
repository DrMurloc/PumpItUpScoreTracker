using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Events;

/// <summary>
///     A board tag has been tied to a site account, authoritatively — the import read the game tag
///     off the authenticated account page, which is the strongest tag-to-account signal there is.
///     <para>
///         The ghost-becomes-real moment (docs/design/rivals.md D5): anything holding the tag as a
///         stand-in for a person can now hold the person instead. Fires on every import, including
///         re-links of a tag that already pointed at this account, so consumers must be idempotent.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerLinkedEvent(MixEnum Mix, string Tag, Guid UserId);
