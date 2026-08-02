using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Contracts.Queries;

/// <summary>
///     What a board tag actually is right now: whether the mirror knows it, whose account it
///     belongs to if any, and how to draw it.
///     <para>
///         The normalization seam (docs/design/rivals.md D7). The account page renders a tag
///         "TAG #1234" and the boards render it "TAG#1234"; only this vertical knows that, so a
///         caller passes whatever it has and stores what comes back. Two normalizers drift, and
///         this codebase has already paid for that twice.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ResolveOfficialPlayerQuery(MixEnum Mix, string Tag)
    : IQuery<OfficialPlayerResolution?>;
