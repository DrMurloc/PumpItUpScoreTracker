namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     The five families the granular piucenter badges group into, owner-defined 2026-07-26
///     and intended to be the site's ubiquitous high-level skill vocabulary.
///     This is deliberately NOT the retired <c>SkillCategory</c>: that one existed to bucket
///     the rollup enum, and its groupings were arbitrary enough to file a drill under Stamina.
///     These group the real badges, every badge belongs to exactly one, and no badge is
///     dropped (docs/design/nuke-old-skill-categories.md §2).
///     A badge's family is identity, not data — it never re-hues per mix.
/// </summary>
public enum BadgeCategory
{
    Brackets,
    Twists,
    StaminaAndRuns,
    Tech,
    DoublesTech
}
