using MediatR;

namespace ScoreTracker.Identity.Contracts.Queries;

// Runs server-side only (the OfficialMirror import handler sends it); returns null when the
// stored credential can't be unlocked (key deleted, rotated, or tampered) so the caller falls
// back to manual entry rather than surfacing an error.
[ExcludeFromCodeCoverage]
public sealed record RevealImportCredentialQuery(Guid KeyId, string Ciphertext)
    : IQuery<RevealedImportCredential?>;
