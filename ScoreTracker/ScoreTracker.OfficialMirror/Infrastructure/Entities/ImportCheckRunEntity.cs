using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     One completeness check. Serves three jobs at once, which is why it is a table rather than
///     a UI setting: it is the monthly ledger the deep-scan limit counts off, the last result the
///     page renders without touching piugame, and the summary a player copies into Discord when
///     they have run out of scans and still disagree.
/// </summary>
internal sealed class ImportCheckRunEntity
{
    [Key] public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MixId { get; set; }
    public DateTimeOffset RanAt { get; set; }

    /// <summary>"census" or "deep" — only a deep scan spends one of the month's three.</summary>
    [MaxLength(20)]
    public string Kind { get; set; } = string.Empty;

    public double OfficialPumbility { get; set; }
    public double LocalPumbility { get; set; }
    public int OfficialPasses { get; set; }
    public int LocalPasses { get; set; }

    /// <summary>
    ///     The findings, serialized. They are a variable-length list read back whole and never
    ///     queried across, so a second table would buy nothing.
    /// </summary>
    public string Findings { get; set; } = "[]";
}
