# Score truth model

Status: **owner-workshopped 2026-07-30, built.** Extended 2026-08-17 by
[stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) (D10–D17), which splits *broken*
into failed-and-finished versus interrupted, narrows D3, and restates D7 — pointers at each. What counts as a personal best, what
counts as a play, which source may lower a record, and where each of those lives. Measurements
in this doc were taken against the prod-synced local database on 2026-07-30.

Companion specs: [phoenix2-import-go-live.md](phoenix2-import-go-live.md) (the P2 import
shape — several of its §3 decisions are superseded here, see §7),
[import-scores-refresh.md](import-scores-refresh.md) (the import page),
[chart-details-overhaul.md](chart-details-overhaul.md) (the journey timeline that reads the
journal).

## 1. Context

Four writers feed one command, and each carries its own hand-written idea of "is this an
improvement." They have drifted apart, and the drift is visible in the data: records that lost
score to a plate-only update, broken attempts wearing plates the game never awarded, and stage
breaks the player walked away from stored as records.

The root cause is that **plate is treated as an independent improvement axis**. Score, plate and
broken-ness are each maxed separately, so a submission can win on one axis and drag the others
backwards with it.

This doc replaces four rules with one, and states what each store is for.

## 2. The model

- **D1 — `PhoenixRecord` holds your best *passing* score.** A broken attempt lives there only
  when you have no pass on that chart, and only when you opted in (D2). A pass outranks a break
  no matter the numbers. *Broken* here means failed-and-finished; a **stage break** — the song
  interrupted — is never a personal best at all, from any source, opt-in or not
  ([stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) D10).

- **D2 — "Record broken scores as your best" is the opt-in**, renamed from "Include Broken
  Scores". It governs whether a break may *become* the record on a chart you have never passed.
  It never lets a break displace a pass.

  **D2a (2026-08-10) — the choice is one account-wide value, and absence means "follow the
  mix."** It was re-derived from the mix on every page load and never written down, so a Phoenix
  2 player unticked it before every import. The default is unchanged and now says why on screen:
  Phoenix 2 keeps a personal best for a failed stage and Phoenix does not, and the site mirrors
  the official one. Absence is stored as absence rather than as the resolved default, so
  "follow the mix" keeps tracking the mix instead of freezing a snapshot of it. `BrokenScorePreference`
  in `Web/Services` is the one resolver; the import page's checkbox writes an explicit choice,
  the widget configurator's three-option select can also write "follow", and the completeness
  check reads it rather than the `false` it used to hardcode.

  **D2b — the opt-in is withdrawable.** Turning it off leaves the records earlier imports
  already made, so Your Data carries a cleanup that removes them
  ([delete-my-data.md §18](delete-my-data.md)). It withdraws the *record* only: the journal keeps
  every play, which is what makes the cleanup re-derivable rather than a deletion — the official
  best list still carries the run.

- **D3 — the official site's My Best Scores page is the source of truth for your best.** The
  import's best-page walk is primary; recently-played is a supplement, never an override — with
  one narrowing (2026-08-17): the Phoenix 2 list freezes the first non-pass attempt, so when the
  card is *broken*, the recent window's best finished fail competes with it through the policy
  and may replace it, broken against broken; a passing card is never touched from the window
  ([stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) D17).

- **D4 — a plate improvement alone is not a personal best.** Plate is a tiebreak at equal score
  and nothing more. It can never pull a score down with it.

- **D5 — recently-played supplies the plays that were not your best**, and supplies the
  judgement breakdown for the ones that were. Non-best plays are journaled; they never touch
  the record.

- **D6 — the journal flags `IsBest`**: true when that row became the record at the moment it
  was written.

- **D7 — a zero-note stage break is never stored, anywhere, for any reason.** Someone started a
  song and let it fail out. PIUGAME records it; we do not. Restated once stage breaks were
  journaled: the walk-off is a stage break with **nothing hit** — only misses — since the life bar
  draining leaves a miss count behind ([stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md) D11).

- **D8 — plate is null on anything that is not a pass.** The game awards no plate on a failed
  stage. Any code deriving one for a broken attempt goes away. This propagates the whole way
  out: the weekly and Daily Step boards keep accepting broken entries, and store a null plate
  for them (owner call, 2026-07-30 — the alternative, refusing broken entries outright, was
  considered and rejected).

- **D9 — manually recorded scores are entered as your current best.** That is the only route by
  which a personal best may decrease, and it covers the four UI record forms, the public API's
  `RecordScore`, and CSV upload. A player may save whatever they want; `Source` is how we tell
  later that a human meant it.

### The precedence policy

One comparison, used everywhere:

```
incoming beats stored when:
  1. stored is broken and incoming is not          → incoming wins   (a pass always outranks a break)
  2. incoming is broken and stored is not          → stored wins     (never the reverse)
  3. otherwise, higher score wins
  4. at equal score, better plate wins             (tiebreak only)
  5. otherwise, no change                          (no record write, no journal row)
```

Rule 5 is the existing progress-only guard and it stays: the import deliberately re-scrapes past
its cutoff, so repeats are expected and must not touch the record, the journal, or `RecordedDate`.

Manual sources (D9) skip the policy entirely and overwrite. Everything else is subject to it.

### Source authority

| Source | May raise a best | May lower a best | Journals non-best plays |
|---|---|---|---|
| `officialImport` | yes, via the policy | no | yes (from recently-played) |
| `csv` | yes, authoritative | **yes** | no |
| `manual` (4 UI forms) | yes, authoritative | **yes** | no |
| `manual` (public API `RecordScore`) | yes, authoritative | **yes** | no |

CSV counts as manual (owner call, 2026-07-30): the file is what the player wants shown. The
page's current promise — *"Only new or improved scores will be saved"* — becomes untrue and its
copy has to say so, because a stale re-upload will now knock scores down.

## 3. Where each thing lives

| Store | Holds | Written when |
|---|---|---|
| `scores.PhoenixRecord` | your current best per (user, chart, mix) — passing unless you have no pass | the policy says the submission wins, or a manual source overwrites |
| `scores.ScoreEventJournal` | every observed play, best or not | on any record change, and on any dated recently-played card |

The journal stays **append-only on the write path**: no importer or handler rewrites history. A player deleting their own data is the sanctioned exception — the rule protects the record from us, not from the person whose plays it is ([delete-my-data.md](delete-my-data.md) D8). `IsBest`
is set at insert. The one-time corrective migration in §6 is a migration, not application
behavior — the same standing as the 2026-06 backfill seed.

### Journal identity

A journal row is one **play**, keyed `(UserId, MixId, ChartId, OccurredAt)` where `OccurredAt` is
the *site's* stamped play time. Both sites stamp every recently-played card, so a re-import of
the same play produces the identical key and collapses.

That key is also why the write is an upsert rather than an insert. One play can arrive twice
inside a single import — once from recently-played as an observation, once from the best page as
the record change (the best-page row already inherits `RecordedAt` from its producing recent
play). Same key, one row, `IsBest` flipped true by the second write.

Cards with no site timestamp are not journaled as non-best plays: there is no safe key and every
re-import would duplicate them.

Measured collision risk before the unique index: **14 rows across 7 keys**, out of 1,039,857.

## 4. Import shape

The two official-site surfaces have distinct, non-overlapping jobs.

**My Best Scores (`my_best_score.php`)** — the record. On Phoenix 1 it lists passes only. On
Phoenix 2 (the redesign) it also lists broken bests, detected by the empty plate slot. Its
displayed date is the chart's *first* play and is not a recency signal
(see [phoenix2-import-go-live.md §2](phoenix2-import-go-live.md) and the 5-page up-score window
in `WalkDatedBestScores`).

**Recently played (`recently_played.php`)** — the plays. It supplies judgement breakdowns,
the Daily Step observation, non-best journal rows, and — when D2 is on — a broken best for a
chart the best page never listed.

Two things it must stop doing:

- **Building a record out of separate maxima.** `Max(score)` across the group with
  `All(broken)` for the flag produces attempts that never happened: one 900k break plus one
  850k pass currently yields *score 900,000, not broken*. The winning **play** is selected by
  the policy; its own score, plate and flag travel together.
- **Deriving a plate for a break.** `ScoreScreen.PlateText` returns `PerfectGame` when every
  judgement count is zero, so a zero-note break mints a Perfect Game. Plate is null when broken.

Cards the site labels `STAGE BREAK` are already skipped, and have been since
`a0a54e72`. D7 extends that to any card whose judgements sum to zero, and to a broken
best-page card scoring 0.

**Note counts were not affected, and are not part of this effort.** Only a pass judges every
note, so only a pass is a valid `UpdateNoteCount` sample — and that already held, because the
`STAGE BREAK` skip drops breaks before they can reach the group. Verified 2026-07-30 against
that day's production backup, comparing `ChartMix.NoteCount` to the judgement sums of journaled
passes: **Phoenix 2 is exact on all 1032 checkable charts**, Phoenix on 2577 of 2579. The two
Phoenix outliers (`Simon Says, EURODANCE!!` S20 stores 938 vs 1005 observed; `Over the Horizon`
S20 stores 980 vs 1000) are short by whole-chart margins rather than mid-song ones, which reads
as a chart re-step the stored count predates — not a partial sample. Unresolved, and small.

`UpdateNoteCount` now takes its sample from a passing play explicitly rather than relying on the
parser upstream to have removed the alternative. That is defence, not a fix: the catalog learns a
note count once and never revisits it, so a wrong value is permanent, and the guard is two lines.

## 5. What the numbers say

Prod-synced local database, 2026-07-30. `PhoenixRecord`: 1,019,684 rows.
`ScoreEventJournal`: 1,039,857 rows (943,102 `backfill`, 92,492 `officialImport`, 2,384 `csv`,
1,879 `manual`).

| Symptom | Rows | Reading |
|---|---|---|
| Records below their journaled peak, plate improved | **12** (8 users, 45,446 points lost) | the plate leak, D4 |
| Records below their journaled peak, any other cause | 3 | manual corrections, intentional |
| Broken records carrying a plate | **30,316** | D8. 92% `Rough Game` — the `PlateText` signature |
| …of those, `manual` | **4** | left alone (D9) |
| Broken records with `Score = 0` | **91** (19 `officialImport`, 72 pre-tracking) | D7 |
| Broken records with `Score` NULL | 540 | *not* D7 — a human left the box blank, which the UI invites. Kept |
| Broken records plated Superb-or-better | 18 | all `backfill`-only, dated 2023–2025, no import path can produce them. Left alone |

The 18 include five at exactly `1,000,000 / Perfect Game / broken`. No importer can produce that
combination — the classic best page never sets broken and the recent parser derives broken from
the *absence* of a plate image, which a Perfect Game card would have. A human typed them. Their
defect is the broken flag, not the plate; nulling the plate would make them worse.

Journal history only reaches back to the 2026-06 backfill, which seeded from `PhoenixRecord`
itself. Regressions older than that are unmeasurable, so "12" means "since June," not "ever."

## 6. Data repair

One SQL script, run once. No admin button — the surfaces are too small.

1. **Plate leak (12 rows).** Restore each record to its peak journaled non-broken score and that
   row's plate.
2. **Zero-note breaks (91 rows).** Delete the `PhoenixRecord` row, and the matching journal rows
   — leaving an orphan `IsBest` journal row would contradict D1.
3. **Plate on a break (30,312 rows).** Set `Plate = NULL` where `IsBroken = 1` and `Source` is
   `officialImport` or NULL. The 4 `manual` rows are left alone.

The journal keeps its fabricated plates otherwise: it is a record of what the site told us, and
rewriting history to match a rule written later would destroy the evidence that found the bug.

## 7. What this supersedes

[phoenix2-import-go-live.md](phoenix2-import-go-live.md), which was already partly stale:

- Its §2 note *"broken detection keys on the empty plate slot, never on `score == 0`"* stays
  true for *detection*, but a broken best scoring 0 is now dropped rather than stored (D7).
- Its §3.3 / §4.5 *"Include Broken Scores defaults ON when mix == Phoenix 2"* keeps the default;
  the control is renamed (D2).
- Its §4.3 *"the KeepBestStats broken-over-pass rule extends to judgements"* is restated by the
  policy: judgements ride the winning play, because everything rides the winning play.
- Its §3.2 / §4.2 incremental-cutoff watermark **was already removed** before this effort — the
  displayed best-page date is the first play, not the latest, so the watermark truncated every
  import after the first. `WalkDatedBestScores` stops on a 5-page up-score window instead.

Docs to update in the same PR:

| Doc | Change |
|---|---|
| [DATABASE-SCHEMA.md](../DATABASE-SCHEMA.md) | `ScoreEventJournal` row: no longer "best-attempt *changes*" — it is every observed play, with `IsBest`. `PhoenixRecord` row: plate null when broken |
| [phoenix2-import-go-live.md](phoenix2-import-go-live.md) | superseded-by pointers on the four items above |
| [import-scores-refresh.md](import-scores-refresh.md) | the checkbox copy deck, and the CSV confirm promise |
| [DOMAIN.md](../DOMAIN.md) | glossary entries for *personal best* and *broken* |
| [API.md](../API.md) | `RecordScore`'s `KeepBestStats` semantics; `ScoreDto.Plate` nullable on the weekly-charts endpoint |
| [daily-step.md](daily-step.md) | broken entries carry no plate |

## 8. Test plan

- **Unit (`DomainTests/`)** — the policy is pure and gets a table-driven `[Theory]`: pass-beats-
  break both directions, score ordering, the equal-score plate tiebreak, and the case that
  started this — better plate + lower score must not win.
- **Component (`ApplicationTests/`)** — `UpdatePhoenixRecordHandlerTests` loses its per-axis
  `KeepBestStats` cases and gains policy ones; a broken submission over a passing record writes
  nothing; a manual submission overwrites downward and journals.
  `OfficialSiteClientTests` gains: a group holding one higher break and one lower pass saves the
  pass; a zero-note card is dropped; a broken card yields a null plate.
- **Integration (`Tests.Integration/`)** — the journal upsert: the same play imported twice
  leaves one row, and the second write flips `IsBest`.
- **Components (`Tests.Components/`)** — `UploadPhoenixScoresPageTests` follows the renamed key.
- **Approval (`ApprovalTests/PiuGameApi/`)** — a fixture with a zero-note card and a broken card,
  pinning "no plate, not parsed."

## 9. Settled by the owner, 2026-07-30

- **CSV is authoritative** (§2 D9). The import page's "only new or improved" promise changes
  with it.
- **The import widget gains a control.** `ImportScoresConfig` takes the opt-in as `bool?` —
  null keeps today's behavior (on for Phoenix 2, off for Phoenix 1), so no config migration.
- **Weekly and Daily Step keep accepting broken entries** and go nullable-plate (D8). The
  narrower "passes only" alternative was rejected.
- **The five `1,000,000 / Perfect Game / broken` records are out of scope.** A human typed
  them; §5 explains why no importer could have. They keep their plates and their flag.

- **`ScoreDto.Plate` is nulled, not blanked.** It goes `string` → `string?` on the
  weekly-charts endpoint, the only consumer of that DTO. A public wire change, accepted: the
  field was already meaningless whenever `IsBroken` was true, and emitting `""` would be a lie
  with extra steps. The `Tests.Api` golden moves with it.
