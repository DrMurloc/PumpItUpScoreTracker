using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Apis.Dtos
{
    public sealed class PiuGameGetUcsResult
    {
        public string SongName { get; set; }
        public ChartType ChartType { get; set; }
        public DifficultyLevel Level { get; set; }
        public string Uploader { get; set; }
        public string Description { get; set; }
    }
}
