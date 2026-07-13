using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts.Messages;

// Bus trigger: run one import off a session id, off the request circuit. Carries the sid, never
// a password.
[ExcludeFromCodeCoverage]
public sealed record RunOfficialImportCommand(Guid UserId, MixEnum Mix, RedactedString Sid, string CardId,
    string ExpectedGameTag, bool IncludeBroken, bool SyncPiuTracker);
