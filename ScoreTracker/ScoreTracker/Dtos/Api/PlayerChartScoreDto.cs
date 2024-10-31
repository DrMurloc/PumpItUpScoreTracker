namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class PlayerChartScoreDto
    {
        public Guid ChartId { get; set; }
        public PlayerDto Player { get; set; }
        public ScoreDto Score { get; set; }
    }
}
