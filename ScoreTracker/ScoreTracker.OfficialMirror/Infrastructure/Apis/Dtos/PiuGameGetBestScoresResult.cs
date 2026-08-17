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

            /// <summary>Null on a broken best — the redesigned page lists failed bests with no plate.</summary>
            public PhoenixPlate? Plate { get; set; }

            public bool IsBroken { get; set; }

            /// <summary>
            ///     A broken best whose grade slot is empty: the stage broke, and the number in
            ///     <see cref="Score" /> is the running score at the moment it did — normalised over
            ///     the notes judged so far, not a chart score. The redesigned list keeps such a
            ///     card as an unpassed chart's first attempt (docs/design/stage-breaks-and-max-combo.md).
            /// </summary>
            public bool IsStageBroken { get; set; }

            /// <summary>When the best was saved. Only the redesigned page shape carries it.</summary>
            public DateTimeOffset? RecordedAt { get; set; }
        }

        public int MaxPage { get; set; }

        /// <summary>
        ///     The page's own "Total." — every chart the list holds, not just this page. Phoenix
        ///     counts passes; Phoenix 2's redesigned list also counts stage breaks. Null when the
        ///     header is absent.
        /// </summary>
        public int? TotalCharts { get; set; }
    }
}
