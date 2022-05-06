namespace ScoreTracker.Web.Dtos;

public sealed class DifficultyOverviewDto
{
    public int Difficulty { get; set; }
    public int ACount { get; set; } = 0;
    public int SCount { get; set; } = 0;
    public int SSCount { get; set; } = 0;
    public int SSSCount { get; set; } = 0;
    public int PassedCount { get; set; } = 0;
    public int UnpassedCount { get; set; } = 0;
}