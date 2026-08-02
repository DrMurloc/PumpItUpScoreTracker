using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Events;

/// <summary>
///     An admin accepted a detected rename: the same human now appears on the boards under a new
///     tag, and their history has been merged onto it.
///     <para>
///         Published so anything storing a tag can follow (docs/design/rivals.md D5). Nothing
///         merges automatically — this only ever follows a human decision, which is why an
///         undetected rename leaves a stored tag pointing at nobody rather than at the wrong
///         person.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record OfficialPlayerRenamedEvent(MixEnum Mix, string OldTag, string NewTag);
