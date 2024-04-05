using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(UserId))]
    [Index(nameof(Title))]
    public sealed class UserTitleEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid UserId { get; set; }
        [Required] public string Title { get; set; }
    }
}
