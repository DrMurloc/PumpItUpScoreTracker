namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetLeaderboardResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        public sealed class Entry
        {
            public string ProfileName { get; set; } = string.Empty;
            public int Rating { get; set; }
        }
    }
}
