using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId))]
    [Index(nameof(UserId))]
    public sealed class SuggestionFeedbackEntity
    {
        public Guid UserId { get; set; }
        [Key] public Guid Id { get; set; }
        public Guid ChartId { get; set; }
        public string SuggestionCategory { get; set; } = string.Empty;
        public bool IsPositive { get; set; }
        public bool ShouldHide { get; set; }
        [Required] public string FeedbackCategory { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
