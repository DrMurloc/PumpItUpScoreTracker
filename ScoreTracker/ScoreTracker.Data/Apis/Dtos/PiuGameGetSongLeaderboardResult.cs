namespace ScoreTracker.Data.Apis.Dtos
{
    public sealed class PiuGameGetSongLeaderboardResult
    {
        public EntryResultDto[] Results { get; set; } = Array.Empty<EntryResultDto>();

        public sealed class EntryResultDto
        {
            public string ProfileName { get; set; } = string.Empty;
            public int Score { get; set; }
        }
    }
}
