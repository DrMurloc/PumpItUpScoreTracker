using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Apis.Dtos
{
    public sealed class PiuGameGetSongLeaderboardResult
    {
        public EntryResultDto[] Results { get; set; } = Array.Empty<EntryResultDto>();

        public sealed class EntryResultDto
        {
            public int Score { get; set; }
        }
    }
}
