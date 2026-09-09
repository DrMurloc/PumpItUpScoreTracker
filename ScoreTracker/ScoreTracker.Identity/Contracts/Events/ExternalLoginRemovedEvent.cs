namespace ScoreTracker.Identity.Contracts.Events;

/// <summary>
///     An account unlinked a sign-in provider. Published because nothing else reports it, and
///     standing granted off the back of a sign-in should not outlive it — community Discord roles
///     hang on a linked account, and without this they stand until the nightly sweep notices.
///     <para>
///         Carries the provider name, not the external id: consumers act on "this account no longer
///         has a Discord", and the id they granted against is theirs to remember. Two strings and a
///         Guid, so nothing can be lost in transit.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ExternalLoginRemovedEvent(Guid UserId, string LoginProviderName);
