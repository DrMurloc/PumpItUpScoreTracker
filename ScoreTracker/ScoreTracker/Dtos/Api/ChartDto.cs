namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class ChartDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int Level { get; set; }
        public SongDto Song { get; set; } = new();
    }
}
