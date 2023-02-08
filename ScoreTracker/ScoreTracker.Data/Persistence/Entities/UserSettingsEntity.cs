using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class UserSettingsEntity
{
    [Key] public Guid UserId { get; set; }

    [Required] public string UiSettings { get; set; } = JsonSerializer.Serialize(new Dictionary<string, string>());
}