using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class ScoreDto
    {
        public ScoreDto(PhoenixScore score, PhoenixPlate? plate, bool isBroken, MixEnum mix = MixEnum.Phoenix)
        {
            Score = score;
            Plate = plate?.ToString();
            LetterGrade = score.LetterGradeFor(mix).GetName();
            IsBroken = isBroken;
        }

        public int Score { get; set; }

        /// <summary>
        ///     NULL when <see cref="IsBroken" /> — the game awards no plate for a failed stage.
        ///     Before 2026-07 this carried a fabricated plate on broken entries.
        /// </summary>
        public string? Plate { get; set; }
        public string LetterGrade { get; set; }
        public bool IsBroken { get; set; }
    }
}
