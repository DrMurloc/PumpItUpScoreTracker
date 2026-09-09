namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Settle every community that hands out Discord roles.
///     <para>
///         The backstop, and the reason this feature survives an in-memory transport at all: any
///         trigger that was dropped, and any change nothing reported — somebody unlinking their
///         Discord, a join the gateway missed — is picked up on the next pass. Reconciling reads
///         current truth, so a sweep with nothing to fix costs reads and writes nothing.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SweepDiscordRolesCommand;
