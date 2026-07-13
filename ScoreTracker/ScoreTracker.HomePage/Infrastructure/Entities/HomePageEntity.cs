using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.HomePage.Infrastructure.Entities;

/// <summary>
///     One of a user's dashboard pages (docs/design/HomePageWidgets/README.md §2.1). Ordinal
///     is the tab order; DefaultMixId is the page-level mix context widgets inherit
///     (D13: widget override → page default → current mix), null = follow current mix.
/// </summary>
[Index(nameof(UserId))]
internal sealed class HomePageEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(64)] public string Name { get; set; } = string.Empty;

    public byte Ordinal { get; set; }

    public bool IsDefault { get; set; }

    public Guid? DefaultMixId { get; set; }
}
