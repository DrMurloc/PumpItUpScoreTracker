namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class RecordPhoenixScoreDto
    {
        public string SongName { get; set; } = string.Empty;
        public string ChartType { get; set; } = string.Empty;
        public int ChartLevel { get; set; }
        public string? Plate { get; set; }
        public int? Score { get; set; }
        public bool IsBroken { get; set; }
    }
}
