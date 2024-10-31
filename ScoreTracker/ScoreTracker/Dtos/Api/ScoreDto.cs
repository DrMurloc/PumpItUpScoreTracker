using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class ScoreDto
    {
        public ScoreDto(PhoenixScore score, PhoenixPlate plate, bool isBroken)
        {
            Score = score;
            Plate = plate.ToString();
            LetterGrade = score.LetterGrade.GetName();
            IsBroken = isBroken;
        }

        public int Score { get; set; }
        public string Plate { get; set; }
        public string LetterGrade { get; set; }
        public bool IsBroken { get; set; }
    }
}
