using MediatR;
using ScoreTracker.Identity.Contracts.Queries;

namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     Whether a player has switched Hardmode on (docs/design/hardmode-leaderboard.md D30). A
///     UiSetting whose ABSENCE means off, so every account starts off and the default costs no row;
///     the switch writes a value to turn it on and clears the key to turn it off.
///     <para>
///         Every surface that asks — the session page, the Discord card, the feed capture — reads it
///         through <see cref="Read" />, so none of them can disagree about what the stored row means or
///         about what a failed read means.
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

    /// <summary>
    ///     The player's switch, read from their settings. A failed read counts as off: the switch only
    ///     decides whether Hardmode is shown on top of everything else, so a transient failure costs the
    ///     Hardmode lines and never the card, the feed row or the page they ride on — and showing
    ///     Hardmode to a player who never asked is the worse way for it to go. Cancellation still throws.
    /// </summary>
    public static async Task<bool> Read(IMediator mediator, Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return IsOn(await mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
