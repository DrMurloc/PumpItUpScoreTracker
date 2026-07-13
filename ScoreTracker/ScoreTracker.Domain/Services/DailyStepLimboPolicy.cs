using System.Globalization;

namespace ScoreTracker.Domain.Services;

/// <summary>
///     When the shared Daily Step chart becomes a "Limbo Day" — once per ISO week, on a
///     (pseudo-)random weekday where the LOWEST passing score wins instead of the highest.
///     Deterministic from the ISO week alone, so exactly one Limbo day lands per week with no
///     persisted oracle: the weekday varies week to week but stays fixed within a week.
/// </summary>
public static class DailyStepLimboPolicy
{
    // ISO weeks run Monday–Sunday; index 0..6 maps to that order.
    private static readonly DayOfWeek[] IsoWeekDays =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    /// <summary>The single Limbo weekday for an ISO (year, week). Pure and stable.</summary>
    public static DayOfWeek LimboDayOfWeek(int isoYear, int isoWeek)
    {
        // A murmur-style finalizer avalanches adjacent weeks onto unrelated days, so the Limbo
        // day doesn't visibly march one weekday forward at a time.
        unchecked
        {
            var h = (uint)(isoYear * 53 + isoWeek);
            h ^= h >> 16;
            h *= 0x7feb352d;
            h ^= h >> 15;
            h *= 0x846ca68b;
            h ^= h >> 16;
            return IsoWeekDays[h % 7];
        }
    }

    /// <summary>Whether the given calendar day is its ISO week's Limbo day.</summary>
    public static bool IsLimboDay(DateTimeOffset date)
    {
        var day = date.Date;
        return day.DayOfWeek == LimboDayOfWeek(ISOWeek.GetYear(day), ISOWeek.GetWeekOfYear(day));
    }
}
