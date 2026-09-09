namespace ScoreTracker.Identity.Contracts.Events;

/// <summary>
///     An account linked a sign-in provider. The mirror of
///     <see cref="ExternalLoginRemovedEvent" />, and needed for the same reason: standing that
///     hangs on a linked account has to start when the link does.
///     <para>
///         Linking is the LAST step of the ordinary onboarding order — join the community, join the
///         Discord (which finds no account yet), then link. Without this, nothing at all fires on
///         that third step and the player waits for the nightly sweep, which they cannot trigger
///         themselves: the page's Check now is admin-only.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ExternalLoginAddedEvent(Guid UserId, string LoginProviderName);
