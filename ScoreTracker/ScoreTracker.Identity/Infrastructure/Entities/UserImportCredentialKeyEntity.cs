namespace ScoreTracker.Identity.Infrastructure.Entities;

// The Key-Vault-wrapped data key for one remembered credential on one device. Holds no
// password: the AES-GCM ciphertext lives in the browser, the master key never leaves the vault.
internal sealed class UserImportCredentialKeyEntity
{
    public Guid KeyId { get; set; }
    public Guid UserId { get; set; }
    public byte[] WrappedDataKey { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; }
}
