namespace ScoreTracker.Data.Apis.Dtos
{
    public sealed class PiuGameGetSongLeaderboardResult
    {
        public EntryResultDto[] Results { get; set; } = Array.Empty<EntryResultDto>();

        public sealed class EntryResultDto
        {
            public Uri AvatarUrl { get; set; } =
                new("https://piugame.com/data/avatar_img/4f617606e7751b2dc2559d80f09c40bf.png", UriKind.Absolute);

            public string ProfileName { get; set; } = string.Empty;
            public int Score { get; set; }
        }
    }
}
