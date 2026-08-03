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
    ///     Set when a new account finishes <c>/Setup</c>. Its presence is the whole "this account
    ///     is past onboarding" test — <c>LoginController</c> reads it to stop sending them back,
    ///     and anything that would interrupt a first-run player reads it to stay quiet.
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