namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Localized application text for message composition outside an HTTP request —
///     Discord cards, replies, and command descriptions. Keys are English UI text
///     verbatim (the resx convention); a null, unknown, or unsupported culture and any
///     missing key both fall back to the English text. The formatted overload applies
///     the target culture to the arguments too, so scores and dates render per-locale.
/// </summary>
public interface ILocalizedTextAccessor
{
    string Get(string? culture, string key);

    string Get(string? culture, string key, params object[] args);
}
