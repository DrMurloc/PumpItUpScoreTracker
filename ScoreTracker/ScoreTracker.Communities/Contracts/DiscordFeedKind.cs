namespace ScoreTracker.Communities.Contracts
{
    /// <summary>
    ///     The broadcast feeds a Discord channel can subscribe to, independent of any
    ///     community registration. Each subscription is per mix.
    /// </summary>
    public enum DiscordFeedKind
    {
        WeeklyCharts,
        DailyStep,
        OfficialLeaderboards
    }
}
