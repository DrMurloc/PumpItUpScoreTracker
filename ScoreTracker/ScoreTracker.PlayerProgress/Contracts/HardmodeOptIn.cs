namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     Whether a player has switched Hardmode on (docs/design/hardmode-leaderboard.md D30). A
///     UiSetting whose ABSENCE means off, so every account starts off and the default costs no row;
///     the switch writes a value to turn it on and clears the key to turn it off.
///     <para>
///         Every surface that asks — the session page, the Discord card, the feed capture — reads the
///         player's settings through <c>GetUserUiSettingsQuery</c> and answers with <see cref="IsOn" />,
///         so none of them can disagree about what the stored row means.
///     </para>
/// </summary>
public static class HardmodeOptIn
{
    public const string SettingKey = "Universal__HardmodeOptIn";

    /// <summary>On when the key carries a value. The settings dictionary is case-insensitive.</summary>
    public static bool IsOn(IDictionary<string, string>? settings)
    {
        return settings != null && settings.TryGetValue(SettingKey, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }
}
