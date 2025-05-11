namespace ScoreTracker.Domain.Events
{
    public sealed record RecentScoreImportedEvent(Guid UserId, RecentScoreImportedEvent.Entry[] Entries)
    {
        public sealed record Entry(Guid ChartId, int Score, string Plate, bool IsBroken);
    }
}
