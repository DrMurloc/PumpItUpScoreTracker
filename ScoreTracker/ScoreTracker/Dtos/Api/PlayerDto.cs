using ScoreTracker.Domain.Models;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class PlayerDto
    {
        public PlayerDto(User user)
        {
            Username = user.IsPublic ? user.Name.ToString() : "Anonymous";
            GameTag = user.IsPublic ? user.GameTag?.ToString() ?? "" : "";
            Country = user.Country?.ToString() ?? "";
            AvatarUrl = user.ProfileImage.ToString();
        }

        public string Username { get; set; }
        public string GameTag { get; set; }
        public string Country { get; set; }
        public string AvatarUrl { get; set; }
    }
}
