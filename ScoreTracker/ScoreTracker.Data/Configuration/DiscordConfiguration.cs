namespace ScoreTracker.Data.Configuration;

public sealed class DiscordConfiguration
{
    public string BotToken { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    ///     Kill switch for the Components V2 score cards: false re-routes every rich send
    ///     to the plain-text fallback without a deploy. Delete after burn-in.
    /// </summary>
    public bool RichScoreMessages { get; set; } = true;
}