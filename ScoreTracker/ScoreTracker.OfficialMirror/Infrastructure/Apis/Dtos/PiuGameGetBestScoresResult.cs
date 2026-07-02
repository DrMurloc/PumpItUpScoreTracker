using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetBestScoresResult
    {
        public ScoreDto[] Scores { get; set; } = Array.Empty<ScoreDto>();

        public sealed class ScoreDto
        {
            public Name SongName { get; set; }
            public DifficultyLevel Level { get; set; }
            public ChartType ChartType { get; set; }
            public PhoenixScore Score { get; set; }
            public PhoenixPlate Plate { get; set; }
        }

        public int MaxPage { get; set; }
    }
}
