namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class MatchDto
    {
        public string Name { get; set; } = "";
        public string Phase { get; set; } = "";
        public string State { get; set; } = "";
        public string[] Players { get; set; } = Array.Empty<string>();
        public string[] Charts { get; set; } = Array.Empty<string>();
        public string[] VetoedCharts { get; set; } = Array.Empty<string>();
        public string[] Winners { get; set; } = Array.Empty<string>();
        public DateTimeOffset? LastStateChange = null;

        public IDictionary<string, IDictionary<string, int?>> Scores { get; set; } =
            new Dictionary<string, IDictionary<string, int?>>();

        public IDictionary<string, IDictionary<string, int>> Points { get; set; } =
            new Dictionary<string, IDictionary<string, int>>();

        public string[] FinalPlaces { get; set; } = Array.Empty<string>();
    }
}
