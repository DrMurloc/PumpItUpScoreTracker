using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities
{
    [Index(nameof(Username))]
    internal sealed class UserOfficialLeaderboardEntity
    {
        [Key] public Guid Id { get; set; }
        public int Place { get; set; }
        public string Username { get; set; }
        public string LeaderboardType { get; set; }
        public string LeaderboardName { get; set; }
        public int Score { get; set; }
    }
}
