using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Web.Dtos.Api
{
    public sealed class PhoenixImportRequestDto
    {
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
        public string GameTag { get; set; } = string.Empty;
        public bool IncludeBroken { get; set; } = false;

        /// <summary>
        ///     Accepted and ignored. Sending a player's session to PIU Tracker is now a share they
        ///     hold on the Community Tools page, not a per-request flag — the players who had this
        ///     on were moved across, so their imports behave exactly as before. The property stays
        ///     because v1 is frozen, not broken: a caller that still sends it gets a 200, not a 400.
        /// </summary>
        public bool SyncScoreTracker { get; set; } = false;
    }
}
