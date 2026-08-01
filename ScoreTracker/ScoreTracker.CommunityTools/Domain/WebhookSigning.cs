using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     Signs a delivery so a maker can prove it came from us.
///     <para>
///         Stripe's scheme, because makers recognise it: the timestamp is <b>inside</b> the signed
///         payload, so a captured body cannot be replayed later under its original signature.
///     </para>
///     <para>
///         Every delivery also carries whatever header the maker asked for. Two mechanisms rather
///         than one because they suit different people — a static header is one <c>if</c> in a
///         handler and is what most tools will actually check, while the signature is there for
///         anyone who wants payload integrity too. Sending both costs nothing.
///     </para>
/// </summary>
internal static class WebhookSigning
{
    public const string SignatureHeader = "X-PIU-Signature";
    public const string DeliveryIdHeader = "X-PIU-Delivery-Id";

    /// <summary>
    ///     The exact bytes a verifier must hash: the timestamp, a dot, and the raw body. A maker who
    ///     re-serializes the JSON before hashing gets a different digest — the single most common
    ///     integration failure, and why the console echoes the signed bytes verbatim.
    /// </summary>
    public static string PayloadToSign(long timestamp, string rawBody)
    {
        return timestamp.ToString(CultureInfo.InvariantCulture) + "." + rawBody;
    }

    public static string Sign(string secret, long timestamp, string rawBody)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(PayloadToSign(timestamp, rawBody)));
        return $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    /// <summary>A tool's signing secret. Shown once when the tool is created, stored as a hash for display only.</summary>
    public static string MintSecret()
    {
        return "whsec_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }
}
