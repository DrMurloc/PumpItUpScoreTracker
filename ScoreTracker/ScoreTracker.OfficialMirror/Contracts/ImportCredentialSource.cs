using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Contracts;

// How an import gets its credential: typed fresh (one-time), or unlocked from the device's
// stored blob (remember-my-password). The Start handler resolves both to a session id.
[ExcludeFromCodeCoverage]
public abstract record ImportCredentialSource;

[ExcludeFromCodeCoverage]
public sealed record TypedCredentialSource(RedactedString Username, RedactedString Password) : ImportCredentialSource;

[ExcludeFromCodeCoverage]
public sealed record StoredCredentialSource(Guid KeyId, string Ciphertext) : ImportCredentialSource;
