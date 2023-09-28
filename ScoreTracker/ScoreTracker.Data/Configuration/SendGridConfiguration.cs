namespace ScoreTracker.Data.Configuration
{
    public sealed class SendGridConfiguration
    {
        public string ApiKey { get; set; }
        public string ToEmail { get; set; }
        public string FromEmail { get; set; }
    }
}