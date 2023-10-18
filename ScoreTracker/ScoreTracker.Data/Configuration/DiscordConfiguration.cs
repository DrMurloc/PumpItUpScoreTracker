namespace ScoreTracker.Data.Configuration;

public sealed class DiscordConfiguration
{
    public string BotToken { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}