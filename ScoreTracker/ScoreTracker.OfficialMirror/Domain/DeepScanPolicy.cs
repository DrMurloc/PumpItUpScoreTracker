namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     How often a player may ask piugame to be walked end to end.
///     <para>
///         Only a BLIND deep scan spends an allowance. A repair the census localised is bounded
///         and evidence-driven — it reads the levels that disagree and nothing else — so it is
///         free. The allowance exists to stop "walk everything" being pressed on repeat, not to
///         ration fixing what we already found.
///     </para>
/// </summary>
internal static class DeepScanPolicy
{
    public const int PerMonth = 3;

    public static int Remaining(int spentThisMonth)
    {
        return Math.Max(0, PerMonth - spentThisMonth);
    }

    /// <summary>
    ///     When the next allowance arrives. A calendar month rather than a rolling window, so the
    ///     panel can name a date instead of explaining a policy.
    /// </summary>
    public static DateTimeOffset NextUnlock(DateTimeOffset asOf)
    {
        return new DateTimeOffset(asOf.Year, asOf.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
    }
}
