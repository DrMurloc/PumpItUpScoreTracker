namespace ScoreTracker.Web.Dtos.Api;

public class TierListEntryDto
{
    public string Category { get; set; } = string.Empty;
    public int Order { get; set; }
    public ChartDto Chart { get; set; }
}