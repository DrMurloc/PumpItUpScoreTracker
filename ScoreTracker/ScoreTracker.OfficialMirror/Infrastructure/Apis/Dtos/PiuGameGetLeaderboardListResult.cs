namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetLeaderboardListResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        public sealed class Entry
        {
            public string Name { get; set; } = string.Empty;
            public string Id { get; set; } = string.Empty;
        }
    }
}
