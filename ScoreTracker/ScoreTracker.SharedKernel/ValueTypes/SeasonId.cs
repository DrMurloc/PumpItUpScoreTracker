using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.SharedKernel.ValueTypes;

/// <summary>
///     Which season a row or a read belongs to: <see cref="AllTime" /> (0) or one calendar quarter,
///     spelled as its number — 20264 is Fall 2026 — so a season sorts by time, reads in raw SQL
///     without a join and costs two bytes in every index that leads with it
///     (docs/design/seasons.md D11, D34). Built only through <see cref="From(int, int)" /> or
///     <see cref="From(short)" />, which reject anything that is not a real quarter; the stored
///     column is the <see cref="Value" />.
/// </summary>
[JsonConverter(typeof(SeasonIdConverter))]
public readonly record struct SeasonId : IComparable<SeasonId>, IFormattable
{
    // Phoenix 2's first season is Summer 2026; nothing earlier is a season. The upper bound is
    // what fits in a smallint.
    private const int FirstYear = 2026;
    private const int LastYear = 3276;

    public static readonly SeasonId AllTime = new(0);

    private readonly short _value;

    private SeasonId(short value)
    {
        _value = value;
    }

    /// <summary>The stored number: 0 for all-time, otherwise year × 10 + quarter.</summary>
    public short Value => _value;

    public bool IsAllTime => _value == 0;

    /// <summary>The calendar year. All-time has none.</summary>
    public int Year => IsAllTime
        ? throw new InvalidSeasonIdException("all-time has no year")
        : _value / 10;

    /// <summary>The calendar quarter, 1–4. All-time has none.</summary>
    public int Quarter => IsAllTime
        ? throw new InvalidSeasonIdException("all-time has no quarter")
        : _value % 10;

    public static SeasonId From(int year, int quarter)
    {
        if (year < FirstYear || year > LastYear)
            throw new InvalidSeasonIdException($"year {year} is outside {FirstYear}–{LastYear}");
        if (quarter is < 1 or > 4)
            throw new InvalidSeasonIdException($"quarter {quarter} is not 1–4");

        return new SeasonId((short)(year * 10 + quarter));
    }

    /// <summary>The stored number back into a season; 0 is <see cref="AllTime" />.</summary>
    public static SeasonId From(short value)
    {
        if (value == 0) return AllTime;
        if (value < 0) throw new InvalidSeasonIdException($"{value} is negative");

        return From(value / 10, value % 10);
    }

    public static implicit operator short(SeasonId season)
    {
        return season._value;
    }

    public int CompareTo(SeasonId other)
    {
        return _value.CompareTo(other._value);
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }

    /// <summary>
    ///     Travels with the type, the way every opaque value type on a bus message must
    ///     (CLAUDE.md "A bus message must survive a JSON round trip"): the number, never an object.
    /// </summary>
    public sealed class SeasonIdConverter : JsonConverter<SeasonId>
    {
        public override SeasonId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return From(reader.GetInt16());
        }

        public override void Write(Utf8JsonWriter writer, SeasonId value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value._value);
        }
    }
}
