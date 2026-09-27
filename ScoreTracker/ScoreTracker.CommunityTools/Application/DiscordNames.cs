using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     The Discord handle behind a linked account, for the maker's console and the review queue.
///     Only the account id is stored, so the handle is asked of the bot and remembered for a while.
///     Null when Discord cannot say — the pages then show the account as linked without a name.
/// </summary>
internal sealed class DiscordNames
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    private readonly IBotClient _bot;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DiscordNames> _logger;

    public DiscordNames(IBotClient bot, IMemoryCache cache, ILogger<DiscordNames> logger)
    {
        _bot = bot;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> HandleOf(string discordId, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(discordId, out var id)) return null;

        var key = $"CommunityTools__DiscordHandle__{id}";
        if (_cache.TryGetValue(key, out string? known)) return known;

        try
        {
            var user = await _bot.GetUser(id, cancellationToken);
            if (user is null) return null;

            _cache.Set(key, user.Username, Lifetime);
            return user.Username;
        }
        catch (Exception e)
        {
            // The bot being down, or never started, costs the page a name — never the page. The
            // failure is not remembered, so the next render asks again.
            _logger.LogWarning(e, "Could not look up Discord user {DiscordId}", id);
            return null;
        }
    }
}
