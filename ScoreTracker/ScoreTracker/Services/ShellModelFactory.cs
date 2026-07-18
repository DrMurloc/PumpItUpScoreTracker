using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Services.Theming;

namespace ScoreTracker.Web.Services;

/// <summary>
///     Builds the static shell's model from the request. Runs while the HttpContext is live, so
///     it is the one place that reads the anonymous mix cookie — a circuit cannot.
/// </summary>
public sealed class ShellModelFactory
{
    private const string MixSettingKey = "Universal__CurrentMix";

    /// <summary>The anonymous mix selection. Signed-in users keep theirs in UiSettings.</summary>
    public const string MixCookieName = "CurrentMix";

    private const string GameTagKey = "GameTag";
    private const string ProfileImageKey = "ProfileImage";

    private const string DefaultAvatar =
        "https://piuimages.arroweclip.se/avatars/4f617606e7751b2dc2559d80f09c40bf.png";

    private static readonly TimeSpan SettingsTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecapTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EventsTtl = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IMediator _mediator;
    private readonly ShellContext _shell;

    private ShellViewModel? _built;

    public ShellModelFactory(IMediator mediator, ICurrentUserAccessor currentUser, IMemoryCache cache,
        ShellContext shell)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _cache = cache;
        _shell = shell;
    }

    /// <summary>
    ///     Memoized for the scope: the host page builds the model to hand the mix to the circuit
    ///     and the layout builds it to render chrome, and the settings read behind it is a
    ///     database call.
    /// </summary>
    public async Task<ShellViewModel> BuildAsync(HttpContext http, CancellationToken cancellationToken = default)
    {
        if (_built is not null) return _built;

        var loggedIn = _currentUser.IsLoggedIn;
        var userId = loggedIn ? _currentUser.User.Id : (Guid?)null;
        var settings = loggedIn
            ? await GetSettings(userId!.Value, cancellationToken)
            : new Dictionary<string, string>();

        var currentMix = ResolveMix(http, settings);
        // The circuit cannot read the request, so every later read of the anonymous mix serves
        // this instead of the cookie.
        _shell.CurrentMix = currentMix;

        // Theme override lives behind /Account, so an anonymous visitor can never have one.
        var themeMix = loggedIn
            ? MixThemes.ResolveThemeMix(Setting(settings, MixThemes.OverrideSettingKey), currentMix)
            : currentMix;

        return _built = new ShellViewModel(
            loggedIn,
            userId,
            loggedIn ? DisplayNameFor(settings) : null,
            Setting(settings, ProfileImageKey) is { Length: > 0 } image ? image : DefaultAvatar,
            currentMix,
            themeMix,
            LegacyMixGate.IsGatedMix(currentMix),
            loggedIn && await HasRecap(userId!.Value, cancellationToken),
            await GetHighlightedEvents(cancellationToken),
            http.Request.Path.HasValue ? http.Request.Path.Value! : "/",
            // Mix switching reloads the page it was invoked from, query and all.
            $"{(http.Request.Path.HasValue ? http.Request.Path.Value : "/")}{http.Request.QueryString}");
    }

    private static string? Setting(IDictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var value) ? value : null;

    private string DisplayNameFor(IDictionary<string, string> settings)
    {
        var name = _currentUser.User.Name.ToString();
        var gameTag = Setting(settings, GameTagKey);
        return string.IsNullOrWhiteSpace(gameTag) ? name : $"{name} ({gameTag})";
    }

    private static MixEnum ResolveMix(HttpContext http, IDictionary<string, string> settings)
    {
        if (Enum.TryParse<MixEnum>(Setting(settings, MixSettingKey), out var saved)) return saved;
        if (Enum.TryParse<MixEnum>(http.Request.Cookies[MixCookieName], out var anonymous)) return anonymous;
        return MixEnum.Phoenix;
    }

    /// <summary>
    ///     Cache key for a user's shell settings. <see cref="UiSettingSavedCacheEviction" />
    ///     evicts it whenever a UI setting is saved, so a mix switch (or theme/game-tag
    ///     change) is visible on the very next request instead of after the TTL.
    /// </summary>
    public static string SettingsCacheKey(Guid userId)
    {
        return $"{nameof(ShellModelFactory)}__Settings__{userId}";
    }

    private Task<IDictionary<string, string>> GetSettings(Guid userId, CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync(SettingsCacheKey(userId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SettingsTtl;
            return _mediator.Send(new GetUserUiSettingsQuery(userId), cancellationToken);
        })!;
    }

    private Task<bool> HasRecap(Guid userId, CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync($"{nameof(ShellModelFactory)}__Recap__{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RecapTtl;
            return await _mediator.Send(new GetPlayerRecapQuery(userId), cancellationToken) != null;
        });
    }

    private Task<IReadOnlyList<TournamentRecord>> GetHighlightedEvents(CancellationToken cancellationToken)
    {
        return _cache.GetOrCreateAsync($"{nameof(ShellModelFactory)}__HighlightedEvents", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = EventsTtl;
            var tournaments = await _mediator.Send(new GetAllTournamentsQuery(), cancellationToken);
            return (IReadOnlyList<TournamentRecord>)tournaments.Where(t => t.IsHighlighted).ToArray();
        })!;
    }
}
