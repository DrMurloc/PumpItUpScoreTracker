using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Apis.Dtos;

public sealed class PiuGameGetRecentScoresResult
{
    public Name SongName { get; set; }
    public DifficultyLevel Level { get; set; }
    public ChartType ChartType { get; set; }
    public PhoenixScore Score { get; set; }
    public PhoenixPlate Plate { get; set; }
    public int NoteCount { get; set; }
    public bool IsBroken { get; set; }
}