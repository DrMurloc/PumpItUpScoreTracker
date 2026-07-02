using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos;

internal sealed class PiuGameGetRecentScoresResult
{
    public Name SongName { get; set; }
    public DifficultyLevel Level { get; set; }
    public ChartType ChartType { get; set; }
    public PhoenixScore Score { get; set; }
    public PhoenixPlate Plate { get; set; }
    public int NoteCount { get; set; }
    public bool IsBroken { get; set; }
}
