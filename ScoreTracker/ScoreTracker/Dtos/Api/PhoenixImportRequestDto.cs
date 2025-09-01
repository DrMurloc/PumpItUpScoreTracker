using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class PhoenixImportRequestDto
    {
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
        public string GameTag { get; set; } = string.Empty;
        public bool IncludeBroken { get; set; } = false;
        public bool SyncScoreTracker { get; set; } = false;
    }
}
