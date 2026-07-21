using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class ScoreDto
    {
        public ScoreDto(PhoenixScore score, PhoenixPlate plate, bool isBroken, MixEnum mix = MixEnum.Phoenix)
        {
            Score = score;
            Plate = plate.ToString();
            LetterGrade = score.LetterGradeFor(mix).GetName();
            IsBroken = isBroken;
        }

        public int Score { get; set; }
        public string Plate { get; set; }
        public string LetterGrade { get; set; }
        public bool IsBroken { get; set; }
    }
}
