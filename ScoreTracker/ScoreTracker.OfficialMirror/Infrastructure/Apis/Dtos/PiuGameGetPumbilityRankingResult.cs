namespace ScoreTracker.OfficialMirror.Infrastructure.Apis.Dtos
{
    internal sealed class PiuGameGetPumbilityRankingResult
    {
        public Entry[] Entries { get; set; } = Array.Empty<Entry>();
        public bool IsEnd { get; set; }

        public sealed class Entry
        {
            public string ProfileName { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public double Pumbility { get; set; }
            public Uri? AvatarUrl { get; set; }
        }
    }
}
