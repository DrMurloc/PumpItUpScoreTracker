namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class TournamentDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
