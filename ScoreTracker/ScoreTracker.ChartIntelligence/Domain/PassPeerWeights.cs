namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     How much one player's pass counts toward a chart's place on the community Pass list of a
///     mix without difficulty titles. A player is placed by their competitive level for the
///     folder's chart type, floored to a whole level, relative to the folder: three, two and one
///     levels below it count 7, 6 and 5, the folder's own level counts 4, and one, two and three
///     levels above it count 1, 2 and 3. Anyone further away counts nothing.
///     <para>
///         At or below the folder a player counts only while active — a best recorded within
///         <see cref="ActivityWindow" />. Above the folder activity is not asked, and the weights
///         rise with distance, so a player three levels stronger counts triple one a single level
///         stronger.
///     </para>
/// </summary>
internal static class PassPeerWeights
{
    /// <summary>How recently a player must have recorded a best to count at or below the folder.</summary>
    public static readonly TimeSpan ActivityWindow = TimeSpan.FromDays(120);

    public static int For(int folderLevel, double competitiveLevel, bool isActive)
    {
        var offset = (int)Math.Floor(competitiveLevel) - folderLevel;
        return offset switch
        {
            -3 when isActive => 7,
            -2 when isActive => 6,
            -1 when isActive => 5,
            0 when isActive => 4,
            1 => 1,
            2 => 2,
            3 => 3,
            _ => 0
        };
    }
}
