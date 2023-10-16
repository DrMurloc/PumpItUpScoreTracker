using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class UserApiTokenEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid UserId { get; set; }
        [Required] public Guid Token { get; set; }
        [Required] public long CurrentTokenUsageCount { get; set; }
        [Required] public long TotalUsageCount { get; set; }
    }
}
