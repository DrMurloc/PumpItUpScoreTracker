using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Configuration;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Clients;

/// <summary>
///     Wraps/unwraps a data key with the credential master key. With a vault configured the wrap
///     runs in Key Vault (RSA-OAEP-256) so the master key never leaves it; otherwise a configured
///     256-bit AES key wraps with AES-GCM (local development and tests).
/// </summary>
public sealed class KeyEnvelope : IKeyEnvelope
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly ConcurrentDictionary<string, CryptographyClient> VaultClients = new();
    private readonly KeyVaultConfiguration _config;

    public KeyEnvelope(IOptions<KeyVaultConfiguration> options)
    {
        _config = options.Value;
    }

    public async Task<byte[]> Wrap(byte[] dataKey, CancellationToken cancellationToken = default)
    {
        if (UsingVault)
        {
            var wrapped = await VaultClient().WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, dataKey, cancellationToken);
            return wrapped.EncryptedKey;
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[dataKey.Length];
        using var aes = new AesGcm(LocalKeyBytes, TagSize);
        aes.Encrypt(nonce, dataKey, cipher, tag);

        var result = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, result, NonceSize + TagSize, cipher.Length);
        return result;
    }

    public async Task<byte[]> Unwrap(byte[] wrappedDataKey, CancellationToken cancellationToken = default)
    {
        if (UsingVault)
        {
            var unwrapped =
                await VaultClient().UnwrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, wrappedDataKey, cancellationToken);
            return unwrapped.Key;
        }

        var nonce = wrappedDataKey.AsSpan(0, NonceSize);
        var tag = wrappedDataKey.AsSpan(NonceSize, TagSize);
        var cipher = wrappedDataKey.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(LocalKeyBytes, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private bool UsingVault =>
        !string.IsNullOrWhiteSpace(_config.VaultUri) && !string.IsNullOrWhiteSpace(_config.KeyName);

    private CryptographyClient VaultClient()
    {
        var keyId = $"{_config.VaultUri!.TrimEnd('/')}/keys/{_config.KeyName}";
        return VaultClients.GetOrAdd(keyId, id => new CryptographyClient(new Uri(id), new DefaultAzureCredential()));
    }

    private byte[] LocalKeyBytes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_config.LocalKey))
                throw new InvalidOperationException(
                    "No credential key configured: set KeyVault:VaultUri + KeyName for production, " +
                    "or KeyVault:LocalKey for local development.");
            return Convert.FromBase64String(_config.LocalKey);
        }
    }
}
