namespace ScoreTracker.Data.Configuration
{
    public class PiuCenterConfiguration
    {
        public string BaseUrl { get; set; } = "https://www.piucenter.com";

        /// <summary>Politeness throttle between per-chart fetches. Default ~1 req/s (design doc §8a).</summary>
        public int RequestDelayMs { get; set; } = 1000;
    }
}
