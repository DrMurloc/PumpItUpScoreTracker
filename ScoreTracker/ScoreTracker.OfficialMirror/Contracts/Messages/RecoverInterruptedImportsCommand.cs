namespace ScoreTracker.OfficialMirror.Contracts.Messages;

/// <summary>
///     Run the restart-recovery pass once (docs/design/import-restart-recovery.md §4).
///     <para>
///         ⚠ Published by a hosted service at process start and by nothing else. There is
///         deliberately no Hangfire job, no timer and no self-rescheduling: the failure this
///         recovers from is the process going away, so the process coming back is the moment to
///         look. Do not add a cadence to it without revisiting that design.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecoverInterruptedImportsCommand;
