using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Apis.Dtos
{
    public sealed class PiuGameGetSongsResult
    {
        public SongDto[] Results { get; set; } = Array.Empty<SongDto>();

        public bool IsEnd { get; set; } = false;

        public sealed class SongDto
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public int Difficulty { get; set; } = 0;
            public string Id { get; set; } = string.Empty;
        }
    }
}
