using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Whether an import may record a run that failed the stage as the player's best.
///     <para>
///         Three surfaces ask this question — the import page, the Import Scores widget and its
///         configurator — and each used to answer it with its own copy of the rule. They are here
///         so they cannot drift.
///     </para>
///     <para>
///         The choice is one account-wide value, and <em>absence</em> is a real answer meaning
///         "follow the mix" — the same presence-encoding the language picker's Automatic uses.
///         Storing the mix default explicitly instead would freeze whatever the default happened
///         to be on the day the player first opened the page, which is not what they chose.
///     </para>
/// </summary>
public sealed class BrokenScorePreference
{
    /// <summary>
    ///     Account-wide, not per page and not per widget: a player who unticks the box on the
    ///     import page and then imports from the dashboard widget means the same thing both times.
    /// </summary>
    public const string SettingKey = "Universal__RecordBrokenAsBest";

    private readonly IUiSettingsAccessor _uiSettings;

    public BrokenScorePreference(IUiSettingsAccessor uiSettings)
    {
        _uiSettings = uiSettings;
    }

    /// <summary>
    ///     What the control reads when the player has never chosen, which mirrors the official
    ///     site rather than expressing a preference: Phoenix 2 keeps a personal best for a failed
    ///     stage and its best-scores list carries them, Phoenix keeps none and its list does not.
    /// </summary>
    public static bool DefaultFor(MixEnum mix)
    {
        return mix == MixEnum.Phoenix2;
    }

    /// <summary>The player's explicit choice, or null when they have never made one.</summary>
    public async Task<bool?> GetChoice(CancellationToken cancellationToken = default)
    {
        var stored = await _uiSettings.GetSetting(SettingKey, cancellationToken);
        return bool.TryParse(stored, out var choice) ? choice : null;
    }

    /// <summary>What an import on this mix should actually do, choice and default resolved.</summary>
    public async Task<bool> AppliesTo(MixEnum mix, CancellationToken cancellationToken = default)
    {
        return await GetChoice(cancellationToken) ?? DefaultFor(mix);
    }

    /// <summary>Records an explicit choice. It outranks the mix default on every mix.</summary>
    public Task Choose(bool recordBroken, CancellationToken cancellationToken = default)
    {
        return _uiSettings.SetSetting(SettingKey, recordBroken.ToString(), cancellationToken);
    }

    /// <summary>
    ///     Drops back to following the mix. Clearing rather than writing the current default is
    ///     what keeps "follow the mix" tracking the mix instead of a snapshot of it.
    /// </summary>
    public Task FollowTheMix(CancellationToken cancellationToken = default)
    {
        return _uiSettings.ClearSetting(SettingKey, cancellationToken);
    }

    /// <summary>Writes a tri-state straight through, for the widget's three-option select.</summary>
    public Task Set(bool? choice, CancellationToken cancellationToken = default)
    {
        return choice == null ? FollowTheMix(cancellationToken) : Choose(choice.Value, cancellationToken);
    }
}
