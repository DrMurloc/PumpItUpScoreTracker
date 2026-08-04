namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Admin trigger: copy the win payloads Communities wrote before the capture moved out
///     (docs/design/rivals.md §4.3) into PlayerProgress's ledger, so feeds keep their history
///     across the cutover instead of starting empty.
///     <para>
///         Lives in Communities because Communities owns the source rows — PlayerProgress cannot
///         reference it back without closing a cycle. One-shot and idempotent: the destination
///         write collides on the event id, so re-running costs a scan and changes nothing.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillPlayerHighlightsCommand;
