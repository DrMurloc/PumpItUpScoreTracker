using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Infrastructure;

internal sealed class ImportCredentialProtector : IImportCredentialProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int DataKeySize = 32;

    private readonly IKeyEnvelope _envelope;
    private readonly IImportCredentialKeyStore _keys;
    private readonly IDateTimeOffsetAccessor _dateTime;

    public ImportCredentialProtector(IKeyEnvelope envelope, IImportCredentialKeyStore keys,
        IDateTimeOffsetAccessor dateTime)
    {
        _envelope = envelope;
        _keys = keys;
        _dateTime = dateTime;
    }

    public async Task<(Guid KeyId, string Ciphertext)> Protect(Guid userId, string username, string password,
        CancellationToken cancellationToken = default)
    {
        var keyId = Guid.NewGuid();
        var dataKey = RandomNumberGenerator.GetBytes(DataKeySize);
        try
        {
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(new CredentialPayload(username, password));
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[plaintext.Length];
            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Encrypt(nonce, plaintext, cipher, tag, AssociatedData(keyId, userId));

            var blob = new byte[1 + NonceSize + TagSize + cipher.Length];
            blob[0] = Version;
            Buffer.BlockCopy(nonce, 0, blob, 1, NonceSize);
            Buffer.BlockCopy(tag, 0, blob, 1 + NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, blob, 1 + NonceSize + TagSize, cipher.Length);

            var wrapped = await _envelope.Wrap(dataKey, cancellationToken);
            await _keys.Save(keyId, userId, wrapped, _dateTime.Now, cancellationToken);
            return (keyId, Convert.ToBase64String(blob));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public async Task<(string Username, string Password)> Unprotect(Guid userId, Guid keyId, string ciphertext,
        CancellationToken cancellationToken = default)
    {
        var wrapped = await _keys.GetWrappedKey(keyId, userId, cancellationToken);
        if (wrapped == null)
            throw new CredentialUnlockException("No stored key for this credential.");

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException e)
        {
            throw new CredentialUnlockException("Malformed credential blob.", e);
        }

        if (blob.Length < 1 + NonceSize + TagSize || blob[0] != Version)
            throw new CredentialUnlockException("Unsupported credential blob.");

        byte[] dataKey;
        try
        {
            dataKey = await _envelope.Unwrap(wrapped, cancellationToken);
        }
        catch (Exception e)
        {
            throw new CredentialUnlockException("Could not unwrap the credential key.", e);
        }

        try
        {
            var nonce = blob.AsSpan(1, NonceSize);
            var tag = blob.AsSpan(1 + NonceSize, TagSize);
            var cipher = blob.AsSpan(1 + NonceSize + TagSize);
            var plaintext = new byte[cipher.Length];
            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Decrypt(nonce, cipher, tag, plaintext, AssociatedData(keyId, userId));

            var payload = JsonSerializer.Deserialize<CredentialPayload>(plaintext)
                          ?? throw new CredentialUnlockException("Empty credential payload.");
            return (payload.U, payload.P);
        }
        catch (AuthenticationTagMismatchException e)
        {
            throw new CredentialUnlockException("Credential failed authentication.", e);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    private static byte[] AssociatedData(Guid keyId, Guid userId)
    {
        return Encoding.UTF8.GetBytes($"piu-cred-v{Version}:{keyId:N}:{userId:N}");
    }

    private sealed record CredentialPayload(string U, string P);
}
