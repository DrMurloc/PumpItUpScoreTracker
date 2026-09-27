using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

// Bus trigger: run one import off a session id, off the request circuit, at the depth the player
// pressed — the Import button, Import and check, or a deep scan. Carries the sid, never a password.
[ExcludeFromCodeCoverage]
public sealed record RunOfficialImportCommand(Guid UserId, MixEnum Mix, RedactedString Sid, string CardId,
    string ExpectedGameTag, bool IncludeBroken, ImportKind Kind = ImportKind.Standard);
