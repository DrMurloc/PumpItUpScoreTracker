using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes
{
    public readonly struct Bpm
    {
        private Bpm(decimal min, decimal max)
        {
            Min = min;
            Max = max;
        }

        private Bpm(decimal value) : this(value, value)
        {
        }

        public override string ToString()
        {
            return Min == Max ? Min.ToString("##.###") : $"{Min:##.###} ~ {Min:##.###}";
        }

        public static implicit operator Bpm(decimal levelInt)
        {
            return From(levelInt);
        }

        public decimal Min { get; }

        public decimal Max { get; }

        public decimal Average => (Min + Max) / 2;

        public static implicit operator Bpm(string bpmString)
        {
            return TryParse(bpmString, out var result)
                ? result
                : throw new InvalidBpmException($"String {bpmString} was mis-formatted for BPM");
        }

        public static bool operator ==(Bpm v1, Bpm v2)
        {
            return v1.Equals(v2);
        }

        public static bool operator !=(Bpm v1, Bpm v2)
        {
            return !v1.Equals(v2);
        }

        public override bool Equals(object? obj)
        {
            switch (obj)
            {
                case Bpm other:
                    return Equals(other);
                default:
                    return false;
            }
        }


        public static bool TryParse(string levelString, out Bpm result)
        {
            result = default;
            if (decimal.TryParse(levelString, out var levelInt))
            {
                result = From(levelInt);
                return true;
            }

            var split = levelString.Split(" ~ ");
            if (split.Length == 2
                && decimal.TryParse(split[0], out var min)
                && decimal.TryParse(split[1], out var max))
            {
                result = From(min, max);
                return true;
            }

            return false;
        }


        private bool Equals(Bpm otherParam)
        {
            return Min == otherParam.Min
                   && Max == otherParam.Max;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public static Bpm From(decimal bpm)
        {
            if (bpm <= 0) throw new InvalidBpmException("BPM must be greater than 0");

            return new Bpm(bpm);
        }

        public static Bpm? From(decimal? min, decimal? max)
        {
            if (min != null && max != null) return From(min.Value, max.Value);
            if (min != null) return From(min.Value);
            if (max != null) return From(max.Value);
            return null;
        }

        public static Bpm From(decimal min, decimal max)
        {
            if (min <= 0) throw new InvalidBpmException("Min BPM must be greater than 0");

            if (max <= 0) throw new InvalidBpmException("Max BPM must be greater than 0");

            if (max < min) throw new InvalidBpmException("Max BPM must be greater than Min BPM");

            return new Bpm(min, max);
        }
    }
}
