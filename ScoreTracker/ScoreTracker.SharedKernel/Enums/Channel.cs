namespace ScoreTracker.SharedKernel.Enums;

/// <summary>
///     The folder a song sits in on the cab's song select (docs/design/song-channels.md §2). Five
///     have ever existed, in this order — the game's own — and a song carries one per mix it is
///     in, because the set a mix offers changes: J-Music ran from Prime through Phoenix and
///     Phoenix 2 folded it into World Music. The enum name is the API token (<c>KPop</c>); the
///     display name comes from <see cref="ChannelHelperMethods.GetName" />.
/// </summary>
public enum Channel
{
    Original,
    KPop,
    WorldMusic,
    JMusic,
    Xross
}

[ExcludeFromCodeCoverage]
public static class ChannelHelperMethods
{
    /// <summary>What the site prints: <c>K-Pop</c>, <c>World Music</c>.</summary>
    public static string GetName(this Channel channel)
    {
        return channel switch
        {
            Channel.Original => "Original",
            Channel.KPop => "K-Pop",
            Channel.WorldMusic => "World Music",
            Channel.JMusic => "J-Music",
            Channel.Xross => "Xross",
            _ => channel.ToString()
        };
    }

    /// <summary>
    ///     The enum name, case-insensitively — what the API's <c>channel</c> parameter and the
    ///     bulk-add JSON's <c>channel</c> field take. False for anything else, a display name
    ///     included: <c>K-Pop</c> is not a token.
    /// </summary>
    public static bool TryParse(string? value, out Channel channel)
    {
        // Name-matched rather than Enum.TryParse, which would also accept "2" as WorldMusic.
        var trimmed = value?.Trim();
        foreach (var candidate in Enum.GetValues<Channel>())
            if (candidate.ToString().Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                channel = candidate;
                return true;
            }

        channel = default;
        return false;
    }
}
