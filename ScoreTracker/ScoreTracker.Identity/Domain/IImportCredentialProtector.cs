namespace ScoreTracker.Identity.Domain;

// Seals/opens a credential with envelope encryption: a random data key encrypts the
// username+password (AES-GCM), that data key is wrapped by the master key (IKeyEnvelope), and the
// wrapped key is stored (IImportCredentialKeyStore). Unprotect throws CredentialUnlockException
// when the stored key is gone or the ciphertext fails authentication.
internal interface IImportCredentialProtector
{
    Task<(Guid KeyId, string Ciphertext)> Protect(Guid userId, string username, string password,
        CancellationToken cancellationToken = default);

    Task<(string Username, string Password)> Unprotect(Guid userId, Guid keyId, string ciphertext,
        CancellationToken cancellationToken = default);
}
