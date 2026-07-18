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
    public int Perfects { get; set; }
    public int Greats { get; set; }
    public int Goods { get; set; }
    public int Bads { get; set; }
    public int Misses { get; set; }

    /// <summary>When the play was saved. Both sites stamp it on recently-played cards.</summary>
    public DateTimeOffset? RecordedAt { get; set; }
}
