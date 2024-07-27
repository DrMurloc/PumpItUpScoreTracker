using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.Domain.ValueTypes;

public readonly struct LevelBucket : IComparable<LevelBucket>
{
    private readonly string _name;

    private static readonly Regex _levelRegex =
        new(@"^([SCDscd]+|)([0-9]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);


    private static readonly IDictionary<string, ISet<(ChartType, DifficultyLevel)>> CachedParses =
        new Dictionary<string, ISet<(ChartType, DifficultyLevel)>>();

    private static ISet<(ChartType, DifficultyLevel)> getDifficulties(string name)
    {
        if (CachedParses.TryGetValue(name, out var parse)) return parse;
        var set = new HashSet<(ChartType, DifficultyLevel)>();
        var sections = name.Split(",").Select(s => s.Trim());
        foreach (var match in sections.Select(s => _levelRegex.Match(s)))
        {
            var typeGroup = match.Groups[1].Value;
            var types = new List<ChartType>();
            if (string.IsNullOrWhiteSpace(typeGroup))
            {
                types.Add(ChartType.Single);
                types.Add(ChartType.Double);
            }

            if (typeGroup.Contains("s", StringComparison.OrdinalIgnoreCase)) types.Add(ChartType.Single);

            if (typeGroup.Contains("d", StringComparison.OrdinalIgnoreCase)) types.Add(ChartType.Double);

            if (typeGroup.Contains("c", StringComparison.OrdinalIgnoreCase)) types.Add(ChartType.CoOp);

            DifficultyLevel.TryParse(match.Groups[2].Value, out var level);
            foreach (var type in types) set.Add((type, level));
        }

        CachedParses[name] = set;
        return set;
    }

    public ISet<(ChartType, DifficultyLevel)> GetDifficulties()
    {
        return getDifficulties(_name);
    }

    private LevelBucket(string nameString)
    {
        _name = nameString;
    }

    public override string ToString()
    {
        return _name;
    }

    public static implicit operator LevelBucket(string nameString)
    {
        return From(nameString);
    }

    public static implicit operator LevelBucket?(string? nameString)
    {
        if (nameString == null) return null;

        return From(nameString);
    }

    public static implicit operator string(LevelBucket value)
    {
        return value._name;
    }

    public static bool operator ==(LevelBucket v1, LevelBucket v2)
    {
        return v1.equals(v2);
    }

    public static bool operator !=(LevelBucket v1, LevelBucket v2)
    {
        return !v1.equals(v2);
    }

    public override bool Equals(object? obj)
    {
        switch (obj)
        {
            case LevelBucket other:
                return equals(other);
            default:
                return false;
        }
    }

    public static bool TryParse(string nameString, out LevelBucket result)
    {
        try
        {
            result = From(nameString);
            result.GetDifficulties();
            return true;
        }
        catch
        {
            result = new LevelBucket("Invalid");
            return false;
        }
    }

    public bool Contains(string value)
    {
        return _name.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private bool equals(LevelBucket otherParam)
    {
        return string.Equals(_name, otherParam._name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return _name.ToLower().GetHashCode();
    }

    public static LevelBucket From(string nameParameterParam)
    {
        if (nameParameterParam == null) throw new InvalidNameException("Name was null.");

        if (string.IsNullOrWhiteSpace(nameParameterParam)) throw new InvalidNameException("Name was empty.");

        return new LevelBucket(nameParameterParam.Trim());
    }

    public static readonly JsonConverter Converter = new NameConverter();

    private class NameConverter : JsonConverter<LevelBucket>
    {
        public override LevelBucket Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return From(reader.GetString() ?? "");
        }

        public override void Write(Utf8JsonWriter writer, LevelBucket value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value._name);
        }
    }

    public int CompareTo(LevelBucket other)
    {
        return string.Compare(_name, other._name, StringComparison.OrdinalIgnoreCase);
    }
}