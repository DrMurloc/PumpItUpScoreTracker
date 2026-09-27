namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     Run the restart-recovery pass once (docs/design/import-restart-recovery.md §0).
///     <para>
///         ⚠ Published by a hosted service a few minutes after process start and by nothing else.
///         There is deliberately no Hangfire job, no timer and no self-rescheduling: the failure this
///         recovers from is the process going away, so the process coming back is the moment to
///         look. Do not add a cadence to it without revisiting that design.
///     </para>
/// </summary>
/// <param name="BootedAt">
///     When this process started. The pass's whole test for "orphaned" — a run that began before
///     this boot belonged to the previous process, which can no longer announce it. Carried on the
///     message rather than read from the clock in the consumer, which would measure the moment the
///     pass RUNS and let an import pressed in the meantime fall on the wrong side of the line.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record RecoverInterruptedImportsCommand(DateTimeOffset BootedAt);
