using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Web.Services.Contracts;

public interface IUiSettingsAccessor
{
    /// <summary>
    ///     Where the selected mix is stored. Exposed because "no stored mix" and "stored
    ///     Phoenix" are different answers to a caller that has to pick a default of its own —
    ///     <see cref="GetSelectedMix" /> collapses them.
    /// </summary>
    const string MixSettingKey = "Universal__CurrentMix";

    /// <summary>
    ///     Set when a new account finishes <c>/Setup</c>, and read by nothing today.
    ///     <para>
    ///         Do not reach for it as a "is this account new?" test. It answers the opposite
    ///         question and only for accounts that postdate the page: every account created before
    ///         <c>/Setup</c> existed lacks the key permanently, so absence means "brand new" and
    ///         "signed up years ago" at the same time. <c>LoginController</c> decides where to send
    ///         a sign-in from whether the account was just created; the one feature that has to
    ///         stay quiet for a first-run player recognises them by their arrival on the page.
    ///     </para>
    ///     <para>
    ///         Kept written because it is the only durable record that an account finished
    ///         onboarding, which is a real question even though nothing asks it yet.
    ///     </para>
    /// </summary>
    const string SetupCompletedSettingKey = "Universal__SetupCompleted";

    Task<MixEnum> GetSelectedMix(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists the selected mix. Symmetric with <see cref="GetSelectedMix" /> so a caller
    ///     inside a circuit never has to know the setting key. It does <em>not</em> write the
    ///     anonymous-fallback cookie — only an HTTP response can — so a full mix switch still
    ///     goes through <c>/Mix/Set</c>; this is for pages that need the choice durable before
    ///     that navigation happens.
    /// </summary>
    Task SetSelectedMix(MixEnum mix, CancellationToken cancellationToken = default);
    Task<string?> GetSetting(string key, CancellationToken cancellationToken = default, Guid? userId = null);
    Task SetSetting(string key, string value, CancellationToken cancellationToken = default);
}