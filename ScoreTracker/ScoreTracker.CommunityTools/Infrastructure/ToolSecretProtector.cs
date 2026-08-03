using System.Security.Cryptography;
using System.Text;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Infrastructure;

/// <summary>
///     Encrypts the one webhook secret that cannot be hashed.
///     <para>
///         The outbound header is sent verbatim on every delivery, so we have to be able to read it
///         back — which rules out the hashing that protects the API keys and the verification
///         secret. AES-GCM under a data key that is itself wrapped by the master key in
///         <see cref="IKeyEnvelope" />, so the database alone is not enough to recover one.
///     </para>
///     <para>
///         The wrapped data key rides inside the blob rather than in a side table. The Identity
///         version keeps a key store because a credential there is re-keyed per user and audited;
///         one column on one row needs none of that, and a second table would be a join and a
///         migration for no property we gain.
///     </para>
/// </summary>
internal sealed class ToolSecretProtector : IToolSecretProtector
{
    private const byte Version = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int DataKeySize = 32;

    private readonly IKeyEnvelope _envelope;

    public ToolSecretProtector(IKeyEnvelope envelope)
    {
        _envelope = envelope;
    }

    public async Task<string> Protect(Guid toolId, string plaintext,
        CancellationToken cancellationToken = default)
    {
        var dataKey = RandomNumberGenerator.GetBytes(DataKeySize);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var cipher = new byte[bytes.Length];
            using (var aes = new AesGcm(dataKey, TagSize))
                aes.Encrypt(nonce, bytes, cipher, tag, AssociatedData(toolId));

            var wrapped = await _envelope.Wrap(dataKey, cancellationToken);

            // [version][wrapped length][wrapped key][nonce][tag][cipher]
            var blob = new byte[1 + 2 + wrapped.Length + NonceSize + TagSize + cipher.Length];
            var at = 0;
            blob[at++] = Version;
            blob[at++] = (byte)(wrapped.Length >> 8);
            blob[at++] = (byte)(wrapped.Length & 0xFF);
            Buffer.BlockCopy(wrapped, 0, blob, at, wrapped.Length);
            at += wrapped.Length;
            Buffer.BlockCopy(nonce, 0, blob, at, NonceSize);
            at += NonceSize;
            Buffer.BlockCopy(tag, 0, blob, at, TagSize);
            at += TagSize;
            Buffer.BlockCopy(cipher, 0, blob, at, cipher.Length);

            return Convert.ToBase64String(blob);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public async Task<string?> Unprotect(Guid toolId, string? ciphertext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ciphertext)) return null;

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException)
        {
            return null;
        }

        if (blob.Length < 1 + 2 || blob[0] != Version) return null;

        var wrappedLength = (blob[1] << 8) | blob[2];
        var at = 3;
        if (blob.Length < at + wrappedLength + NonceSize + TagSize) return null;

        var wrapped = blob[at..(at + wrappedLength)];
        at += wrappedLength;
        var nonce = blob[at..(at + NonceSize)];
        at += NonceSize;
        var tag = blob[at..(at + TagSize)];
        at += TagSize;
        var cipher = blob[at..];

        var dataKey = await _envelope.Unwrap(wrapped, cancellationToken);
        try
        {
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(dataKey, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, AssociatedData(toolId));
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            // A blob that will not authenticate is one that was moved between tools or altered.
            // Returning null sends a delivery out without the header, which the maker's server
            // rejects — loud in the right place rather than a decrypt of somebody else's secret.
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    /// <summary>
    ///     Binds the ciphertext to its tool, so a row copied to another tool fails to authenticate
    ///     rather than quietly decrypting.
    /// </summary>
    private static byte[] AssociatedData(Guid toolId)
    {
        return Encoding.UTF8.GetBytes($"tool-outbound-header:{toolId:D}");
    }
}
