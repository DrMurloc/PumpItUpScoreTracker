# Nuke the old skill categories

The site carries two skill vocabularies. One is real: the **granular piucenter badges** —
33 keys with measured per-chart coverage, crawled and banked as metrics. The other is a
**rollup invented on top of it**: the `Skill` enum (17 values) and `SkillCategory` (5
buckets), produced by a lossy mapping table. This doc is the plan for deleting the rollup.

Owner ruling (2026-07-26): *"any place using the old skill system right now is giving shit
results… The old skills were meaningless and arbitrarily defined. They have weird overlap
that doesn't make sense. All of that logic was supposed to get fixed when we switched to the
piucenter skills, and it annoys me that instead we ended up keeping the chabala skills."*
Result changes are accepted — tier order and Pumbility numbers are expected to move.

## 1. Why the rollup makes results worse

The damage is all in [`PiuCenterSkillMapper.TheirsToOurs`](../../ScoreTracker/ScoreTracker.Catalog/Domain/PiuCenterSkillMapper.cs),
a 33 → 11 collapse:

- **One observation votes more than once.** `bracket_drill → {Brackets, Drills}`,
  `bracket_jump → {Brackets, Jumps}`, `bracket_twist → {Brackets, Twists}`. The pooled
  model treats each mapped skill as an independent observation, so a single chart inflates
  evidence mass *and* correlates two skills that are then averaged as if independent.
- **Signal is destroyed by collapsing.** Five distinct twist badges (`twist_90`,
  `twist_over90`, `twist_close`, `twist_far`, `twists`) become one `Twists`; three run
  badges become `Runs`; **ten badges become `Technical`**. The mapper's own comment concedes
  they had to withhold `doublestep` and `side3_singles` or `Technical` would have covered
  76% of the catalog — that is a taxonomy admitting it does not work.
- **Six of seventeen values are unreachable.** `VeryFast`, `Fast`, `Moderate`, `Slow`,
  `EndRun`, `Gimmicks` have no mapping at all — dead vocabulary that still renders in menus.
- **Weights are guesses.** `ChartSkillChipRecord.Weight` is `SegmentFraction ?? 0.5`. The
  granular badges carry a real `badge_fraction:` coverage in [0,1].
- `SkillCategory` additionally hardcodes five hex colours in SharedKernel, bypassing the
  theme tokens (the `UiColorTokenTests` ratchet only scans `Pages/`, `Components/`, `Shared/`).

**The algorithm itself is fine and stays.** `TierListBlendBuilder.ComputeSkillEvidence`
pools "your deviation from your own folder baseline" per skill, weighted by folder decay ×
chip weight × score age. Nothing in it depends on the key being an enum. The whole migration
is: change the dictionary key from `Skill` to the badge string, and take the weight from
measured coverage instead of a 0.5 default. Same maths, honest inputs, no fan-out.

## 2. The replacement vocabulary

| Source | Meaning |
|---|---|
| `badge_fraction:<badge>` metric | segment coverage in [0,1] — becomes the observation **weight** |
| `top3:<badge>` metric | dominance pick — becomes **Highlighted** (the SRP's match rule) |
| `PiuCenterBadges.DisplayName` | English label, Title-Case fallback for unknown keys |

Two label tables exist today: `Catalog/Domain/PiuCenterBadges.cs` (feeds the SRP) and
`Web/Services/SimilarityBadgeLabels.cs` (feeds the similar-charts shelf). They should merge;
`PiuCenterBadges` is the better home because it sits with the vocabulary. Note the shelf's
deliberate ruling that **badge names are not localized** — every locale renders the English
term, exactly as `Skill.GetName()` already did.

The similar-charts algorithm is **already pure granular** (`GetChartBadgeCoverageQuery` →
`badge_fraction:`) and needs no change. It is the reference implementation.

## 3. Full inventory

### Domain
| Change | Detail |
|---|---|
| Delete `SharedKernel/Enums/Skill.cs` | 17 values, `GetName`, `GetPrimaryCategory` |
| Delete `SharedKernel/Enums/SkillCategory.cs` | 5 buckets + the 5 hardcoded hex colours |
| `SharedKernel/Models/Chart.cs` | drop `IReadOnlySet<Skill> Skills` — positional param, **15 construction sites (9 in tests)** |
| `Catalog/Domain/PiuCenterSkillMapper.cs` | delete `TheirsToOurs`, `MapTheirSkill`, the `ChartSkillsRecord` path; keep `Normalize` + thresholds |
| `Catalog/Domain/PiuCenterBadges.cs` | canonical badge vocabulary; `CategoryFor` goes with `SkillCategory` |
| Delete `Domain/Records/ChartSkillsRecord.cs` | |
| `Domain/Records/PumbilityProjection.cs` | `SkillAdjustmentRecord(Skill,…)` → `(string Badge,…)` |

### Application
| Change | Detail |
|---|---|
| `Catalog/SkillsSaga.cs` | `ChartSkillChipRecord(Skill, Highlighted, SegmentFraction)` → `ChartBadgeChipRecord(string Badge, string DisplayName, bool Highlighted, double Coverage)`; fold `GetChartSkillChipsQuery` into the badge query |
| `ChartIntelligence/TierListBlendBuilder.cs` | `WeightsFor` → `(string, double)` from real coverage; `pooled` / `skillDeviations` re-keyed; **delete the `chart.Skills` fallback** |
| `ChartIntelligence/Contracts/PlayerSkillDeviations.cs` | `IReadOnlyDictionary<string, SkillDeviationRecord>` |
| `ChartIntelligence/PlayerSkillDeviationsHandler.cs`, `PersonalizedBreakdownHandler.cs`, `Contracts/PersonalizedTierListBreakdown.cs` | re-key |
| ~~`PlayerProgress/PumbilityProjectionSaga.cs`~~ | **Done, by deletion.** The PUMBILITY rewrite dropped the skill adjustment entirely rather than re-keying it — the estimator takes no argument describing the player it predicts for (pumbility-overhaul.md §4.3). Nothing to migrate. |
| `Catalog/PiuCenterCrawlSaga.cs` | stop writing abstract skills; metrics only |

### Infrastructure
| Change | Detail |
|---|---|
| `Catalog/Infrastructure/EFChartRepository.cs` | remove 4 `Enum.Parse<Skill>` sites, the `ChartSkill` write path, `Chart.Skills` hydration |
| Delete `ChartSkillEntity.cs`, `ChartSkillArchiveEntity.cs` | + both `ToTable` lines in `CatalogModelContribution` |
| Migration | drop `ChartSkill` and `ChartSkillArchive` (the archive is already unread — its own comment says so) |
| `Data/Apis/PiuCenterDataParser.cs` | drop abstract-skill parsing |
| `EFUcsRepository`, `EFXXChartAttemptRepository`, `OfficialSiteClient`, `XXScoreFile` | drop the empty-set argument (all four already pass `new HashSet<Skill>()`) |

### Presentation
| Change | Detail |
|---|---|
| `Components/SkillCoverageBars.razor` | badge label + coverage; `skillcat-` tint gone. **Only two consumers: the dialog and the chart page** |
| `Components/ChartDetailsDialog.razor` | bars from badges; delete the `Chart.Skills` fallback chips |
| `Pages/ChartDetails.razor` | badge chips query |
| `Components/FolderSkillList.razor`, `TierListChartCard.razor`, `VerdictSentence.razor` | re-key |
| `Pages/TierLists/ChartSkills.razor` (6 sites, 3 of them `chart.Skills` readers), `PersonalizedBreakdown.razor` (15), `Progress/Pumbility.razor` | re-key |
| `Pages/Charts.razor`, `Components/ChartSearchCard.razor` | drop the `skillcat-` tint |
| `Services/Theming/MixThemes.cs`, `wwwroot/css/site.css` | remove `--skillcat-*` tokens + 7 rules |
| `Services/SimilarityBadgeLabels.cs` | merge into `PiuCenterBadges` |
| `Resources/App.*.resx` ×9 | retire the abstract skill-name keys |

### Not affected
`Communities/BotCommandSaga.cs`, `RecommendedChartsSaga.cs`, the title lists
(`PhoenixSkillTitle`, `XXSkillTitle`, …) and `ChartSimilarityCalculator.cs` match only on
the *word* "Skill" — a tier-list lens name, a title name, or prose. The tier-list blend
source stays **named** "Skill"; only its key type changes.

## 4. Commit order

| # | Commit | Layer |
|---|---|---|
| N1 | SRP loses the `SkillCategory` tint | Presentation |
| N2 | Drop `ChartSkillArchive` — entity, contribution, migration | Infrastructure |
| N3 | `ChartBadgeChipRecord` + badge chips query (**additive**; the old path stays alive) | Application |
| N4 | Display swaps to badges: coverage bars, dialog, chart page | Presentation |
| N5 | Tier-list Skill lens re-keyed; `PlayerSkillDeviations` contract; breakdown handler + page | Application |
| N6 | PUMBILITY projection re-keyed | Application/Domain |
| N7 | `ChartSkills.razor`, `FolderSkillList`, `TierListChartCard`, `VerdictSentence` | Presentation |
| N8 | Delete `Skill`, `SkillCategory`, `ChartSkillsRecord`, the mapper fan-out, `Chart.Skills` | Domain |
| N9 | Drop the `ChartSkill` table, crawl writes, parser + migration | Infrastructure |
| N10 | Arch-test ratchet against reintroduction, l10n ×9, docs | tests/docs |

N3 is additive, so N4–N7 can move one surface at a time and every commit stays green.

## 5. Risks

- **Threshold recalibration (N5) is the real risk, not correctness.** `MinSkillEvidence` and
  `MinUsableSkills` were tuned against evidence mass inflated by the fan-out, over 11 keys
  rather than 33. After the swap, mass per key drops and folders may fall back to
  `Unrecorded` more often. Recalibrate against the prod-synced local database with an
  `ExplorationTests` probe before N5 lands — that is what that workbench is for.
- **`Chart.Skills` removal (N8) is a positional-record change** to the most widely
  constructed type on the site. Mechanical, but it touches 15 sites.
- **Post-deploy presses**: Backfill User Tier Lists, Recalculate Ratings (PUMBILITY), and a
  chart-verdict rebuild. Tier order and Pumbility numbers will move — that is the intent.

## 6. Shipped

**Round 2 — DONE (2026-08-25).** The rollup is gone: `Skill`, `SkillCategory`, `Chart.Skills`,
the 33→11 mapper, the queries and records that served it, the write path that regenerated tags,
and the `--skillcat-*` tokens. `scores.ChartSkill` moved to the `archive` schema;
`scores.ChartSkillArchive` stayed live as the Chabala lens's one read. What replaced it is
specified in [chart-identity.md](chart-identity.md) and built alongside. `RetiredSkillRollupTests`
is the ratchet that keeps it dead — it grew back once as a display vocabulary after being
deleted as a data source, which is exactly what a ratchet is for.

**Round 1 — the three chart surfaces (N1, N3, N4).** Scope reduced by the owner to what the
chart list, the chart details page and the chart details dialog need to be accurate; the
scoring paths (N5, N6) and the deletions (N8, N9) are deliberately still pending, so the
`Skill` enum, `GetChartSkillChipsQuery` and the `ChartSkill` table all remain in place and
the tier lists, Personalized Breakdown and Pumbility keep reading them unchanged.

## 7. As decided 2026-08-25 — the plan that supersedes §4's tail

Owner ruling: finish the nuke ("The 11 skills… I want them gone"), with corrections to the
original plan and a replacement identity system specified in
[chart-identity.md](chart-identity.md). Where this section contradicts §3/§4/§5 above, this
section wins.

**Moot since the plan was written** — do not build N5/N6:
- The tier-list blend has **no skill source** (went with Personalized Pass; Score personalizes
  through the peer projection alone) and the PUMBILITY projection dropped its skill nudge
  (measured 0.071 correlation, PeerEstimator doc). §5's recalibration risk no longer exists.
- `PersonalizedTierListBreakdown` still carries the dead fields (`BreakdownSkillRecord`,
  `SkillSourceActive`, `SkillWeight`, `SimilarPlayersWeight`, …) hardwired empty — delete them
  and the page's never-rendering markup (absorbs pumbility-tier-list.md §10's deferred pass).

**Corrections to the deletion steps:**
- **Tables are never dropped** (owner standard, postdates this doc): the N2/N9 `DropTable`s
  become `ALTER SCHEMA TRANSFER` for `scores.ChartSkill` → `archive`.
- **`scores.ChartSkillArchive` stays LIVE in `scores`** — owner: "unarchive it for that" — it is
  the read behind the Chabala lens (below). deletions-wave-1's note that it rides N2 into
  `archive` is void.
- **The Chabala lens keeps the 11, and only it**: while `Ranked by: Chabala` is active, cards
  swap their identity chips for the archived hand tags (a published Catalog query over
  `ChartSkillArchive`), rendered as **neutral grey chips** — tinting them would map the 11 onto
  the families, which is the association the ruling bans. Post-flip charts show no chips there.
  The archive is never written again. Every other lens shows identity chips.
- **Fast / Slow / EndRun die with the mapper** (owner-confirmed): they were derived tags with no
  badge counterpart. The Speed grouping carries the axis now.
- **The verdict re-key** (§3's `VerdictSentence` row) is implemented by reading the identity
  engine's crux facet, not by a mechanical enum→string swap — see chart-identity.md §6.
- **resx orphan keys are left in place** (deletions-wave-2 §2 policy — their own pass, never a
  rider). Several enum-name keys are shared with badge display names; retirement needs
  call-site checks.
- `MixCapabilities.HasSkillData` has zero callers — delete.
- Deletion order caveat: remove the **port members** (`IChartRepository.GetChartSkills` /
  `SaveChartSkills`) before their EF implementations — reflection DI hides an impl-first
  mistake until runtime.

**What replaces the rollup** — built in the same effort, specified in
[chart-identity.md](chart-identity.md): the Speed tier list, the four-kind folder-relative
chip system with its crux metrics and folder baselines, chip-driven family filing for
Group By Skill, and the chart page/dialog adoption. That doc's §8 golden examples are the
acceptance bar for the whole system.
