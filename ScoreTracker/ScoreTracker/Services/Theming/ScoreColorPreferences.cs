using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Web.Services.Contracts;

namespace ScoreTracker.Web.Services.Theming;

/// <summary>
///     The viewer's two peer settings — who their peers are and how their scores are painted —
///     read once per circuit and shared by every <c>PeerScore</c> on the page. Circuit-scoped and
///     memoized like <c>CommunityGlowReader</c>: a page of forty scores must not read the settings
///     forty times. In-app navigation is a full document load, so a save on <c>/Account</c> is seen
///     by the next page without any eviction here; the dialog updates this instance directly for
///     the page it is on.
/// </summary>
public sealed class ScoreColorPreferences(IUiSettingsAccessor settings, ICurrentUserAccessor currentUser)
{
    private Task<Loaded>? _load;

    public async Task<ScoreColorSettings> GetColors() => (await Load()).Colors;

    public async Task<PeerSourceSelection> GetSources() => (await Load()).Sources;

    /// <summary>Writes both settings and keeps this circuit's copy in step with what was written.</summary>
    public async Task Save(ScoreColorSettings colors, PeerSourceSelection sources)
    {
        await settings.SetSetting(ScoreColorSettings.SettingKey, colors.Serialize());
        await settings.SetSetting(PeerSourceSelection.SettingKey, sources.Serialize());
        _load = Task.FromResult(new Loaded(colors, sources));
    }

    private sealed record Loaded(ScoreColorSettings Colors, PeerSourceSelection Sources);

    private Task<Loaded> Load()
    {
        return _load ??= ReadAsync();
    }

    private async Task<Loaded> ReadAsync()
    {
        if (!currentUser.IsLoggedIn) return new Loaded(ScoreColorSettings.Default, PeerSourceSelection.Default);
        var colors = ScoreColorSettings.Parse(await settings.GetSetting(ScoreColorSettings.SettingKey));
        var sources = PeerSourceSelection.Parse(await settings.GetSetting(PeerSourceSelection.SettingKey));
        return new Loaded(colors, sources);
    }
}
