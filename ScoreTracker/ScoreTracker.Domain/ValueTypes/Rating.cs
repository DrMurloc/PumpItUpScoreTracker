using System.Text.Json;
using System.Text.Json.Serialization;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes
{
    public readonly struct Rating : IComparable<Rating>
    {
        private readonly int _score;

        private Rating(int score)
        {
            _score = score;
        }

        public static Rating Min = new(0);

        public override string ToString()
        {
            return _score.ToString();
        }

        public static implicit operator Rating(int scoreInt)
        {
            return From(scoreInt);
        }

        public static implicit operator int(Rating value)
        {
            return value._score;
        }

        public static bool operator ==(Rating v1, Rating v2)
        {
            return v1.Equals(v2);
        }

        public static bool operator !=(Rating v1, Rating v2)
        {
            return !v1.Equals(v2);
        }

        public static bool operator >(Rating v1, Rating v2)
        {
            return v1._score > v2._score;
        }

        public static bool operator <(Rating v1, Rating v2)
        {
            return v2 > v1;
        }

        public override bool Equals(object? obj)
        {
            switch (obj)
            {
                case Rating other:
                    return Equals(other);
                default:
                    return false;
            }
        }


        public static bool TryParse(string scoreString, out Rating? result)
        {
            if (string.IsNullOrWhiteSpace(scoreString))
            {
                result = null;
                return true;
            }

            var couldParse = TryParse(scoreString, out Rating value);
            result = value;
            return couldParse;
        }

        public static bool TryParse(string scoreString, out Rating result)
        {
            result = default;
            return int.TryParse(scoreString, out var scoreInt) && TryParse(scoreInt, out result);
        }

        public static bool IsValid(int scoreInt)
        {
            return TryParse(scoreInt, out _);
        }

        public static bool TryParse(int scoreInt, out Rating result)
        {
            try
            {
                result = From(scoreInt);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }


        private bool Equals(Rating otherParam)
        {
            return _score == otherParam._score;
        }

        public override int GetHashCode()
        {
            return _score.GetHashCode();
        }

        public static Rating From(int score)
        {
            if (score < Min._score) throw new InvalidScoreException("Rating must be greater than 0");

            return new Rating(score);
        }

        public static JsonConverter Converter = new RatingScoreConverter();

        private class RatingScoreConverter : JsonConverter<Rating>
        {
            public override Rating Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return From(reader.GetInt32());
            }

            public override void Write(Utf8JsonWriter writer, Rating value, JsonSerializerOptions options)
            {
                writer.WriteNumberValue(value._score);
            }
        }

        public int CompareTo(Rating other)
        {
            return _score.CompareTo(other._score);
        }
    }
}
