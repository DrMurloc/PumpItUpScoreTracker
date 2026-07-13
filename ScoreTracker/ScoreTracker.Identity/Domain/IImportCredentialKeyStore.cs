namespace ScoreTracker.Identity.Domain;

// Stores the wrapped data key for a remembered credential, keyed by (keyId, userId). Reads are
// always user-scoped so a stolen keyId can't fetch another account's key. Deletion revokes.
internal interface IImportCredentialKeyStore
{
    Task Save(Guid keyId, Guid userId, byte[] wrappedDataKey, DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetWrappedKey(Guid keyId, Guid userId, CancellationToken cancellationToken = default);

    Task Delete(Guid keyId, Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default);

    Task DeleteAll(CancellationToken cancellationToken = default);
}
