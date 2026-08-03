namespace ScoreTracker.CommunityTools.Domain;

/// <summary>
///     The headers every delivery carries.
///     <para>
///         Authenticity is the maker's own header, sent verbatim over TLS — one <c>if</c> in their
///         handler. An earlier revision also signed each body with HMAC-SHA256; that was a crypto
///         layer we owned for no benefit this audience could use, and it went (owner, 2026-08-02).
///     </para>
///     <para>
///         <see cref="DeliveryId" /> is the **dedupe key**. We retry, so the same delivery can arrive
///         twice; a maker who records the id and skips one they have seen is idempotent for free.
///     </para>
/// </summary>
internal static class WebhookHeaders
{
    public const string DeliveryId = "X-PIU-Delivery-Id";
}
