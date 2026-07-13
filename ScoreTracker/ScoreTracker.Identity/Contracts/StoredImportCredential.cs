namespace ScoreTracker.Identity.Contracts;

// The two pieces the client stores in browser local storage: an opaque key id (references the
// wrapped key row) and the base64 AES-GCM ciphertext of the credential.
[ExcludeFromCodeCoverage]
public sealed record StoredImportCredential(Guid KeyId, string Ciphertext);
