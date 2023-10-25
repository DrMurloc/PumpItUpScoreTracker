namespace ScoreTracker.Data.Configuration
{
    public sealed class SendGridConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ToEmail { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
    }
}