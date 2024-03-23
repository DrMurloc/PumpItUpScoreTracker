using ScoreTracker.Domain.Records;

namespace ScoreTracker.Web.Models
{
    public sealed class RatingColorSelector
    {
        public RatingColorSelector(IEnumerable<CommunityLeaderboardRecord> records)
        {
            var rArray = records.ToArray();
            TotalScale = new RatingScale(rArray.Select(s => (int)s.TotalRating));
            SkillScale = new RatingScale(rArray.Select(s => (int)s.SkillRating));
            SinglesScale = new RatingScale(rArray.Select(s => (int)s.SinglesRating));
            DoublesScale = new RatingScale(rArray.Select(s => (int)s.DoublesRating));
            CoOpScale = new RatingScale(rArray.Select(s => (int)s.CoOpRating));
        }

        public RatingScale TotalScale { get; }
        public RatingScale SkillScale { get; }
        public RatingScale SinglesScale { get; }
        public RatingScale DoublesScale { get; }
        public RatingScale CoOpScale { get; }

        public sealed class RatingScale
        {
            private readonly int _redRating;
            private readonly int _greenRating;
            private readonly int _blueRating;
            private readonly int _whiteRating;

            public RatingScale(IEnumerable<int> ratings)
            {
                var ordered = ratings.Where(r => r != 0).OrderByDescending(r => r).ToArray();
                if (!ordered.Any())
                {
                    _redRating = _greenRating = _blueRating = _whiteRating = 0;
                    return;
                }

                _whiteRating = ordered[^1];
                _blueRating = ordered[0];
                _greenRating = ordered[ordered.Length / 3];
                _redRating = ordered[2 * ordered.Length / 3];
            }

            public string GetColor(int rating)
            {
                if (rating < _whiteRating || rating == 0) return "#FFFFFF";
                if (rating >= _whiteRating && rating < _redRating)
                {
                    if (_redRating == _whiteRating) return "#FF0000";

                    var red = (int)(255 * (_redRating - rating) / (double)(_redRating - _whiteRating));
                    return $"#FF{red:X2}{red:X2}";
                }

                if (rating < _greenRating)
                {
                    if (_redRating == _greenRating) return "#00FF00";

                    var green = (int)(255 * (_greenRating - rating) / (double)(_greenRating - _redRating));
                    return $"#{green:X2}{255 - green:X2}00";
                }

                if (rating < _blueRating)
                {
                    if (_blueRating == _greenRating) return "#0000FF";
                    var blue = (int)(255 * (_blueRating - rating) / (double)(_blueRating - _greenRating));
                    return $"#00{blue:X2}{255 - blue:X2}";
                }

                return "#0000FF";
            }
        }
    }
}
