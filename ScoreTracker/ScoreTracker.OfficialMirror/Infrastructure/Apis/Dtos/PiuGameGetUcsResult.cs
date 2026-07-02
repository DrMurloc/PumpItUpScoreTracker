using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetUcsResult
    {
        public string SongName { get; set; }
        public ChartType ChartType { get; set; }
        public DifficultyLevel Level { get; set; }
        public string Uploader { get; set; }
        public string Description { get; set; }
    }
}
