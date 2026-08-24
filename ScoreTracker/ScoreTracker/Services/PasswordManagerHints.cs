using System.Collections.Generic;

namespace ScoreTracker.Web.Services;

/// <summary>
///     <c>UserAttributes</c> bundles that tell a password manager what a masked or credential
///     field is.
///     <para>
///         MudTextField renders its input with no <c>name</c> and no <c>autocomplete</c>, and
///         without those most managers cannot anchor a fill — the field is simply invisible to
///         them. One shared vocabulary keeps the attribute values identical on every surface,
///         which is what lets the entry a manager saved from one page fill all of them:
///         managers key entries on our domain, not on the individual field.
///         <c>PasswordFieldAutofillTests</c> enforces that every password-type field declares
///         one of these.
///     </para>
/// </summary>
public static class PasswordManagerHints
{
    /// <summary>The piugame.com account-name half of the credential pair.</summary>
    public static readonly Dictionary<string, object> PiuGameUsername = new()
    {
        { "name", "username" },
        { "autocomplete", "username" }
    };

    /// <summary>
    ///     The piugame.com password half — always an existing password
    ///     (<c>current-password</c>), never a new one: no surface here creates piugame accounts.
    /// </summary>
    public static readonly Dictionary<string, object> PiuGamePassword = new()
    {
        { "name", "password" },
        { "autocomplete", "current-password" }
    };

    /// <summary>
    ///     A masked field that is not a login — webhook secrets, API tokens. Asks managers not
    ///     to fill a saved login into it or offer to save what gets typed; the masking is for
    ///     shoulders, not for credential storage.
    /// </summary>
    public static readonly Dictionary<string, object> NotALogin = new()
    {
        { "autocomplete", "off" }
    };
}
