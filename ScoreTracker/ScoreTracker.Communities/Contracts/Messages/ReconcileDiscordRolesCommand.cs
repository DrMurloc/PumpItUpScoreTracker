namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Settle a community's Discord roles: one member when <see cref="UserId" /> is set, the whole
///     roster when it is null.
///     <para>
///         A bus trigger rather than an inline call so joining a community never waits on Discord's
///         REST API. Nothing is carried but ids — a reconcile reads current truth rather than
///         applying a delta, so a message that arrives late, twice, or out of order still lands on
///         the right answer, and one that is dropped is picked up by the sweep.
///     </para>
///     <para>
///         Two Guids and nothing else, deliberately: an opaque value type on a bus message
///         serializes to <c>{}</c> and arrives as <c>default</c> with no error at all (CLAUDE.md).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReconcileDiscordRolesCommand(Guid CommunityId, Guid? UserId = null);
