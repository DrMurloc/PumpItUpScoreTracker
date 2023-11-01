using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScoreTracker.Data.Apis.Dtos;

namespace ScoreTracker.Data.Apis.Contracts
{
    public interface IPiuGameApi
    {
        Task<PiuGameGetSongsResult> Get20AboveSongs(int page, CancellationToken cancellationToken);
        Task<PiuGameGetSongLeaderboardResult> GetSongLeaderboard(string songId, CancellationToken cancellationToken);
    }
}
