
using Microsoft.OpenApi;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class ChartDto
    {
        public ChartDto(Chart c)
        {
            Id = c.Id;
            Level = c.Level;
            Type = c.Type.GetDisplayName();
            Shorthand = c.DifficultyString;
            Song = new SongDto
            {
                Name = c.Song.Name,
                Type = c.Song.Type.GetDisplayName(),
                ImagePath = c.Song.ImagePath.ToString()
            };
        }

        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Shorthand { get; set; } = string.Empty;
        public int Level { get; set; }
        public SongDto Song { get; set; } = new();
    }
}
