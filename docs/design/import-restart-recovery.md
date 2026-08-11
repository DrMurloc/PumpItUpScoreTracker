# Import Restart Recovery

Status: **designed** 2026-08-09, not built. Scoped to one failure: **the process going away
mid-import**. A consumer throwing on a healthy process is a different problem with a different
answer (retries hung off the import-status work) and is deliberately out of scope here.

> **Restart only** (owner call, 2026-08-09). Every mechanism below keys off "the app stopped".
> Nothing here is a general retry framework, and it must not grow into one by accident.

> **No session key and no password is written down** (owner call, 2026-08-09). A run interrupted
> mid-scrape is **not resumed** — it is closed, and the player is told to press the button again.
> The credential blob lives in the player's browser and PIU Scores holds only the key that unwraps
> it, so there is nothing server-side to resume from and nothing new is stored to create one. This
> is the same standing rule `SessionModeNeverPersistsBodyTests` ratchets for the delivery path.

## 1. The problem

An import writes its scores durably as it goes — best-attempt rows and journal rows, one per
changed score. Everything *derived* from those scores happens later, off an in-memory batch that a
restart erases.

`UpdatePhoenixBestAttemptCommand` adds each changed score to `PlayerScoreBatchAccumulator` and
schedules a drain for `ScoreBatchPolicy.HoldWindow` (2 min) past the **latest** score in the batch.
The drain publishes one `PlayerScoresUpdatedEvent`. Everything downstream hangs off that event.

Three restart windows, and they are not equally bad:

| Window | Scores | Derived work | What the player sees today |
|---|---|---|---|
| Before the consumer picks up `RunOfficialImportCommand` | none | none | nothing at all — the `ImportResult` row is not open yet |
| During the scrape | partial, durable | never runs for the partial batch | `ImportResult` never closes → "never reported back" on the import strip |
| After the scrape, inside the hold window | **complete, durable** | **never runs** | **"Completed", with an accurate score count** |

The third is the dangerous one. `RunOfficialImportConsumer` closes the run in its `finally` after
the last score is written but **before** the batch drains, so the run reports success and then the
derived work silently never happens:

- no `PlayerScoresUpdatedEvent` → no highlight flags, no folder lamps, no folder-level rows
- no rating step → no Pumbility recalc, no rating milestones, no CompetitiveImprover flags
- no title step → no completions, no paragon gains, no title progress deltas
- no `ScoreHighlightsCapturedEvent` → **no Discord session card**
- no `sessions.Touch` → `ScoreSession.ScoreCount` stays 0 forever behind real journal rows
  (observed 2026-08-08: a run with 7 journal rows and a `ScoreCount` of 0)
- `UserTierListSaga` never re-materializes → the player's personalized tier list keeps the previous
  import's rows until their next import

The scores are right. Everything built on them is missing, and the UI says it worked.

### 1.1 The safety net that covers the other failure

`flush-overdue-score-batches` (every 5 min) does not cover a **restart**: it iterates
`IPlayerScoreBatchAccumulator.Dump()`, the same in-memory dictionary the restart just cleared.

> **Delete the flush job** (owner call, 2026-08-09). It compensates for nothing, and a recurring
> job that appears to cover a failure it cannot cover is worse than no job.
>
> **Reversed, and the job restored** (owner call, 2026-08-11, after a live incident). The reasoning
> above was sound about restarts and wrong about everything else. What it dismissed —
> *"a batch whose scheduled drain was lost inside a process that was still running"* — is a real
> failure that then happened: over roughly eleven hours, **every** session site-wide was left with
> zero counts and a null `ProcessedAt` while imports reported success, with no restart, no deploy
> and not one exception logged. Nothing looks for that, because the pass in §4 runs only at boot
> and the process never rebooted. The job is back on `*/5`, and it carries the mid-life half of the
> recovery described in §4.3.

`ScoreBatchPolicy.WorkExpectedWithin`'s doc comment names both halves.

## 2. What is already durable

| Thing | Where | Written when |
|---|---|---|
| `ImportResult` row | OfficialMirror | `Open` before any piugame call; `Close` in the consumer's `finally` |
| `ScoreSession` row | ScoreLedger | on the submission that mints the session |
| Best-attempt record | ScoreLedger | per changed score |
| `ScoreEventJournal` row | ScoreLedger | per changed score |

Lost on restart: the in-memory bus (any in-flight message, including the scheduled drain), the
batch accumulator, and the concurrency guard. Losing the guard is correct — it is what lets the
player start a fresh import immediately.

## 3. The anchor

**`ImportResult.FinishedAt`.** Written after the scrape and after every score submission, but
before the drain. Because the drain deadline is measured from the *latest* score in the batch,
`FinishedAt` and the batch's deadline are effectively the same instant.

It is not, however, compared against a clock — see §3.0. `FinishedAt` answers *what happened to
the run* (did it report an ending, or did something take it away mid-scrape); `StartedAt` against
the boot instant answers *whether its batch can still be alive*. Both are needed and they answer
different questions.

### 3.0 ⚠ Age is not the test — the boot is

> **Field-observed 2026-08-10, and the first shape of this design got it wrong.** The pass
> originally skipped any run younger than `WorkExpectedWithin`, reasoning that its batch might
> still drain. That guard excluded **exactly the runs this feature exists for**: a run the restart
> itself killed is, at the moment the pass runs, seconds old. Three interrupted runs sat at a null
> outcome with the notice never firing, and nothing would have looked at them again until the next
> restart.
>
> At boot the accumulator is empty. Nothing from the previous process can drain, however recently
> it ran. So the test is **"did this run begin before this process did"**, not "how old is it" —
> and the boot instant is stamped by the publisher in `StartAsync` rather than read from the clock
> in the consumer, so a genuinely live import cannot land on the wrong side of it while the bus
> gets round to the message.
>
> `WorkExpectedWithin` is still the right constant for a *reader* deciding how long to keep
> waiting. It was never the right one for deciding what is orphaned.

### 3.1 Start from the marker, not from the clock

**Order matters here, and getting it backwards costs a magic number.** Ask ScoreLedger for
**unprocessed sessions** *first*, then let OfficialMirror gate each one on its own run:

1. `ScoreLedger.Contracts.Queries.GetUnprocessedSessionsQuery` — sessions with no `ProcessedAt`.
   Tiny by construction: §4.1's backfill marks all history processed, so this is normally empty and
   at worst a handful.
2. For each, OfficialMirror finds its own run by `SessionId` and decides:

| Run state | Action |
|---|---|
| started before this boot, `FinishedAt` set | replay |
| started before this boot, `FinishedAt IS NULL` | replay **and** close `Interrupted` |
| started at or after this boot | skip — it is live, and its batch is really in memory (§3.0) |
| no run at all | skip — a manual, CSV or API session (§3) |

Driving it the other way — enumerate runs by time, then ask whether each session is processed —
does not work. `FinishedAt` and `ProcessedAt` live in different verticals, so OfficialMirror cannot
filter on the marker in its own query, and *every run ever completed* matches the time predicate.
An earlier draft bolted a 24-hour floor on to bound that; the floor was compensating for
enumerating from the wrong end, and it disappears once the tiny set leads.

Anchoring on the import table scopes recovery to imports **by construction**: manual entry, CSV
upload and API submissions never mint an `ImportResult`, so a restart still loses their derived
work. Accepted (owner, 2026-08-09) — a small use case, and the journal-latest fallback is an
extension rather than a redesign if it ever matters.

## 4. The startup recovery pass

> **The boot pass has no scheduled job behind it.** `RecoverInterruptedImportsConsumer`'s
> `RecoverInterruptedImportsCommand` handler runs **once per process start** and never again until
> the next boot. The recurring job in §4.3 is a separate trigger with a separate gate; this half is
> boot-only.

An `IHostedService` in `Web/HostedServices/` publishes one message in `StartAsync`. One consumer
handles it. That is the entire trigger mechanism for the restart case.

> **No new recurring Hangfire job** (owner call, 2026-08-09). Recurring jobs accumulate debugging
> surface that is easy to forget about. Startup is when the restart happened, so startup is when
> the recovery belongs.
>
> **Amended 2026-08-11**: still true of the *restart* case, which is why this pass stays boot-only.
> But "the interruption is always a restart" was the load-bearing assumption, and it broke — see
> §1.1. Recovery from a non-restart interruption cannot wait for a boot that may be weeks away,
> so §4.3 adds the recurring half.

Consequence, stated plainly: recovery from a restart happens at the next boot — which is immediate,
since the restart *is* the boot. An interruption that is **not** a restart is §4.3's business.

The consumer lives in **OfficialMirror**, which owns import runs. It reads its candidates the way
§3.1 describes, and for each one it decides to act on:

1. **Replay** — send `ScoreLedger.Contracts.Commands.ReplaySessionCommand(UserId, SessionId)`.
   ScoreLedger re-checks its own marker and no-ops if the work already ran.
2. **Close, if it never closed** — stamp `Outcome = Interrupted`, which raises the dialog (§7).

Both halves run for a mid-scrape interruption, and that matters more than it first appears: a run
killed mid-scrape still saved real scores into a real session, and **re-importing will not recover
their derived work** — the records already match, so `recordChanged` is false and those charts
never re-enter a batch. Without the replay on that path, their highlights and lamps are lost
permanently even after the player does exactly what the dialog asks.

### 4.1 The processed marker

`ProcessedAt` on the **session** (ScoreLedger), not on the import run — the session is what gets
replayed, and it is the only identifier the capture chain carries.

**ScoreLedger stamps it itself, by consuming `ScoreHighlightsCapturedEvent`.** The obvious design —
`HighlightCaptureSaga` sending a `MarkSessionProcessedCommand` — is impossible:

> `PlayerProgress` references only Domain, Identity, Data, Catalog and ChartIntelligence. It cannot
> reference ScoreLedger, because `ScoreLedger → Communities → PlayerProgress` and the reference
> would close a cycle; `PlayerProgress → OfficialMirror` closes the same one. Both stamp routes out
> of PlayerProgress are illegal, and the compiler will say so.

Consuming the event inverts the direction into one that is already legal, and needs no new port.
Verified 2026-08-09: PlayerProgress's full transitive closure is Domain, Identity, Data, Catalog
and ChartIntelligence, none of which reach ScoreLedger — so `ScoreLedger → PlayerProgress` is
acyclic. No architecture test forbids a vertical referencing a vertical (several already do;
`VerticalBoundaryTests` checks public surface and consumer registration, not the reference graph),
and .NET fails the build on a cycle, so the compiler is the guarantee here.
The event is a sound completion signal because `HighlightCaptureSaga` publishes it
**unconditionally** — "ALWAYS, even with zero flags", each inner step being failure-isolated — so
a session whose chain ran always gets stamped, even when the chain found nothing to say.

**The migration backfills every existing session as processed.** Without it, the first boot after
deploy sees the entire history as unprocessed and tries to replay all of it. "Unprocessed" must
never be able to mean "predates the feature".

### 4.3 The mid-life sweep

The boot pass answers "the process died". `flush-overdue-score-batches` (every 5 minutes, §1.1)
answers the other one: **the process is fine and the drain still never happened.** Same
`FlushOverdueScoreBatchesCommand`, two consumers, covering the two places the work can be sitting:

| Where the work is | Consumer | What it does |
|---|---|---|
| Batch still in the accumulator, past its deadline | `UpdatePhoenixRecordHandler` (ScoreLedger) | Takes it and publishes, exactly as the drain would have |
| Batch gone, session still unprocessed | `RecoverInterruptedImportsConsumer` (OfficialMirror) | Replays it from the journal, as §5 |

**Staleness is the gate here, and the boot instant is not** — the exact mirror of §3.0. Mid-life
every run started after the boot, so the boot test would skip every one of them.
`WorkExpectedWithin` is the constant that answers *has this had its chance*, and past it a live
batch cannot still be holding, because the deadline is two minutes from the run's last score. This
is the use its own doc comment names.

Two guards keep the halves from colliding:

- **The drain half waits out `DrainBuffer`.** Inside that slack the real drain is in flight;
  claiming the batch there only races a publish already on its way.
- **A session an open batch still holds is filtered out of `GetUnprocessedSessionsQuery`.**
  ScoreLedger owns the accumulator, so it answers this itself and OfficialMirror never has to see
  it. At boot the accumulator is empty, so this filters nothing and §4 is unaffected.

**The sweep never closes a run.** A run with no `FinishedAt` is either still scraping — a deep scan
legitimately outlives this window before its first batch exists — or it died with its process,
which is the boot pass's candidate. Marking it `Interrupted` on a five-minute timer would raise the
§7 dialog underneath a player mid-import. Only §4's boot pass closes.

`/Admin` carries the same publish as a **Flush Stuck Score Batches** button, for a report that
arrives before the next tick. Idempotent: it touches only batches already past their deadline and
sessions the ledger still calls unprocessed, so a press with nothing wrong changes nothing.

## 5. Reconstruction

`SessionReplayBuilder`, a **pure domain function** in ScoreLedger: journal rows in,
`PlayerScoresUpdatedEvent.ScoreChange[]` out. Pure so the fidelity risk is unit-testable with no
database.

Inputs: `GetSessionEntries(userId, sessionId)` for what the session wrote, and
`GetChartHistories(userId, chartIds)` for each chart's full history — the row immediately before
the session's row is the "before" state.

The rules it must reproduce, all of them from `UpdatePhoenixRecordHandler`:

| Rule | Why |
|---|---|
| Only new-passes and upscores | The journal records **every** change, but the batch only ever held these two. A plate-only improvement is journaled and was deliberately never announced. |
| `IsNewPass` = previously broken or absent, now not broken | Mirrors `isNewScore` |
| `OldScore` = the previous row's score, upscores only | Mirrors `isUpscore` |
| One entry per chart, new-pass wins | The accumulator keeps `NewCharts` and `UpscoreCharts` disjoint |
| Observation rows excluded (`IsBest == false`) | They never became a record, so they never entered a batch. `IsBest` maps through `EFScoreJournalRepository.Map`, so it is available. |
| **Filter the histories to the session's mix first** | See the warning below |

> ### ⚠ `GetChartHistories` is cross-mix
>
> A returning song carries **one `ChartId` across Phoenix and Phoenix 2**, and the query filters on
> `UserId` and chart id only. So the histories come back with both mixes' plays interleaved, and a
> builder that takes "the row before this session's row" without filtering by mix will hand a
> Phoenix 1 play to a Phoenix 2 session as its before-state — wrong `OldScore`, wrong `IsNewPass`,
> silently.
>
> This is not hypothetical: the implementation carries a scar comment saying the undo replay
> already trusted the opposite and was wrong. **`IScoreJournalRepository`'s XML doc still states
> the opposite** — "Chart ids are mix-scoped by construction, so no mix filter is needed" — and is
> stale. Correcting it is part of this PR, because the next person to write a replay will read the
> interface, not the implementation.

### 5.1 What the replay reads for the "after" state

The drain does **not** read the journal for the new score — `PublishScoreEvents` reads the current
best-attempt records and builds `ScoreChange` from those. The replay reuses that same path, so the
journal is used only to decide *which* charts moved and what the old score was. Reusing the path
rather than re-deriving it is what keeps the two in agreement.

### 5.2 Counts

The replay reuses the drain's publish path so `ScoreSession` counts land too — but **absolutely,
not additively**. `Touch` adds, which is correct when a session drains as several batches; on the
crash-mid-chain path `Touch` has already run and adding again would double the count. The replay
computes the true totals from the journal and sets them.

## 5.3 Indexes

Verified against the prod-synced database 2026-08-09. Sizes: `ScoreSession` 561 rows,
`ImportResult` 6, `ScoreEventJournal` 1,104,414.

| Read | Covered today? |
|---|---|
| `GetSessionEntries` — journal by session | ✅ `IX_ScoreEventJournal_SessionId`, filtered `WHERE SessionId IS NOT NULL` |
| `GetChartHistories` — journal by user + charts | ✅ `IX_ScoreEventJournal_UserId_ChartId_OccurredAt` |
| Unprocessed sessions | ⚠ scan — but of 561 rows, and after the backfill it matches ~0. Add a filtered index `WHERE ProcessedAt IS NULL` anyway: it stays near-empty forever and matches the pattern already used on the journal. |
| `ImportResult` by `SessionId` | ⚠ no index — the table has only `PK` and `(UserId, StartedAt)`. Six rows today, but it grows once per import press. Add `IX_ImportResult_SessionId`. |

Both additions are small and go in the same migration as the columns.

## 6. Replay safety

Running the chain twice is mostly free — `UpsertFlags` upserts, `SaveFolderLevels` overwrites,
ratings recompute, and `UserTierListSaga` documents itself as idempotent. The two append-shaped
writes are `EFPlayerMilestoneRepository.Append` (fresh `Guid.NewGuid()` per row) and
`PlayerHighlightCapturer` (keyed on the event id, which a replay re-mints).

**`ProcessedAt` is the idempotency mechanism, and nothing else is needed.** The recovery pass runs
once at boot, single-threaded, and skips any session already marked. A session whose chain never
ran has no milestones to duplicate; a session whose chain ran is not replayed.

The residual window is a crash in the seconds between the milestones being appended and the stamp
being written. Accepted: worst case is a handful of duplicate lamp rows on one session, visible on
the Sessions page and fixable by hand.

> ### ⚠ Do not add a unique index on `PlayerMilestone`
>
> Checked against the prod-synced database 2026-08-09 before writing the migration, and the
> proposed natural key `(UserId, MixId, Kind, SessionId, Title, Detail)` was **wrong** — 86 groups
> covering ~277 rows already violate it, and none of them is a duplicate.
>
> A session drains as **many batches** (the hold window is two minutes, measured from the latest
> score in the batch), and each batch mints its own rating milestone. One session on 2026-08-06
> produced ten consecutive `PumbilityGain` rows, each with a real `OldValue`→`NewValue` step:
> 0→10624, 10624→11898, 11898→12845, and so on. `PumbilityGain`, `SinglesPumbilityGain`,
> `DoublesPumbilityGain`, `SinglesCompetitiveGain`, `DoublesCompetitiveGain` and
> `OfficialPumbilityRank` all carry NULL `Title` **and** NULL `Detail`, so the key had nothing left
> to tell the batches apart. Creating that index would have failed the deploy, and "fixing" the
> data to let it through would have deleted an hour of a player's real progression.
>
> Adding `OccurredAt` to the key does disambiguate them (verified: zero collisions). It still
> should not be built — a replay stamps its own `OccurredAt`, so the key would not dedupe the one
> case it exists for, and it would buy a migration risk for nothing.

No "card already sent" marker either, for the same reason: `ProcessedAt` covers it, and the stamp
lands immediately after the event is published. If a duplicate card is ever observed in the field,
a Communities-owned marker table is the follow-up — not a cross-vertical write to the session row.

## 7. The interrupted-run dialog

A `MudDialog` island in `MainLayout`, shown once on the next page load with the flag flipped as it
opens. **`DeletionNoticeHost.razor` is the model, not `RecapPointer`** — it is the render-nothing-
until-relevant variant, gating the whole dialog behind `@if (_pending is not null)`, which is this
dialog's shape too. `MainLayout` already hosts three such islands (`RecapPointer`,
`DeletionNoticeHost`, `CommunityToolsAnnouncement`); this is a fourth line beside them, and it must
carry `@rendermode="RenderModes.Interactive"` like the others, because the layout itself renders
statically.

> **Modal on next page load** (owner call, 2026-08-09), and explicitly interim — it becomes a
> notification once a notifications system exists. Not a banner.

It fires for **one** state: a run closed `Interrupted` and not yet acknowledged. A run whose
derived work the recovery pass restored says nothing, because nothing was lost and the notice would be
noise.

Copy (variant A, chosen 2026-08-09):

> **Your import didn't finish**
> PIU Scores restarted while your scores were still importing. Everything it saved is already in —
> but anything it hadn't reached yet is still missing. Importing again picks up whatever didn't
> land.
> `[Import again]` `[Dismiss]`

Two deliberate choices: it never says "failed" (the run was cut short and the scores it saved are
real, so "failed" sends people hunting for damage that is not there), and **Import again navigates
rather than starting a run**, because the password is in the player's browser.

`AcknowledgedAt` on the `ImportResult` row rather than a UiSetting — per-run by construction, so a
second interruption shows again without a key-naming scheme.

`ImportOutcome` gains `Interrupted`. The enum's existing doc comment says there is deliberately no
member for "never came back" because that is the *absence* of an outcome; that stays true for a run
the recovery pass has not reached yet. `Interrupted` is its verdict once it has.

## 8. Duplicate deliveries

Replay re-fires `PlayerScoresUpdatedEvent`, so `WebhookDeliverySaga` sends a subscribed tool the
same import twice.

> **Allowed and disclosed, not suppressed** (owner call, 2026-08-09). Suppressing it means a tool
> silently misses an import that PIU Scores recovered — invisible on both sides. A duplicate is
> visible, cheap to handle, and the payload already carries what is needed to recognise a repeat.

An outlined `Severity.Info` `MudAlert` in the Delivery `ConsoleCard` of `ToolWebhookPanel.razor`,
shown whenever the mode is not `None`:

> **A delivery can arrive twice.** If PIU Scores restarts mid-import, that import is sent again
> once it comes back. Key your handler on the chart and the score's recorded time rather than on
> each delivery being unique. PIUGame session deliveries are the exception — those are never
> re-sent, because the session is gone by then.

Session mode genuinely cannot replay: `WebhookDeliverySaga` skips `WebhookMode.PiuGameSession`
outright, because that mode is delivered inline during the scrape where the sid exists. A matching
line belongs in `docs/API.md` beside the webhook payload description.

## 9. Deliberately not covered

- **Resuming a mid-scrape run.** No sid, no password, by design (§0).
- **A consumer throwing on a healthy process.** Different failure, different fix.
- **Manual / CSV / API batches.** Out of reach of the import-table anchor (§3).
- **A crash inside `HighlightCaptureSaga` on a process that then keeps running.** The stamp is
  absent, so the next boot recovers it — but not before.

## 10. Ownership

| Vertical | What it owns here |
|---|---|
| **OfficialMirror** | the startup recovery pass, `ImportOutcome.Interrupted`, `AcknowledgedAt`, the stale-run reads, the dialog's query and command |
| **ScoreLedger** | `SessionReplayBuilder`, `ProcessedAt`, `GetUnprocessedSessionsQuery`, `ReplaySessionCommand`, the `ScoreHighlightsCapturedEvent` consumer that stamps the marker, the absolute-count write |
| **PlayerProgress** | nothing — it cannot reach either vertical (§4.1), and needs no change |
| **CommunityTools** | nothing — behaviour is unchanged, only the panel copy |
| **Web** | the startup hosted service, the dialog island, the disclaimer, nine locales |

No vertical reads or writes another's tables. Every crossing runs **down** the existing reference
graph — OfficialMirror sends ScoreLedger's contracts, ScoreLedger consumes PlayerProgress's
published event — so no cycle is closed. ScoreLedger reaches PlayerProgress transitively through
Communities today; make the reference direct rather than leaning on that.

⚠ **Both new consumers must be added to their vertical's `AddXxxConsumers(IRegistrationConfigurator)`
hook** — `AddScoreLedgerConsumers` for the stamp consumer, `AddOfficialMirrorConsumers` for the
recovery pass. MassTransit's assembly scan skips internal types, so a consumer that is merely
written is never registered, and every suite stays green while nothing runs
(`VerticalBoundaryTests` tripwires this, and CommunityTools once shipped 33 unregistered handlers
this way).

## 11. Tests

- `DomainTests/SessionReplayBuilderTests` — every rule in §5: a plate-only change is journaled but
  not replayed; a chart that goes new-pass then upscore in one session counts once, as a new pass;
  broken→passed is a new pass; observation rows are excluded; and **a shared-`ChartId` song whose
  history contains plays from both mixes resolves its before-state from the session's own mix**.
  That last one is the §5 warning, and it is the case most likely to be got wrong twice.
- `ApplicationTests` — the recovery pass picks the right candidates and skips live runs; a processed
  session no-ops; a mid-scrape run is both closed and replayed.
- `Tests.Integration` — the migration's backfill leaves no historical session unprocessed, and a
  replayed session's counts are **set** rather than added (§5.1).
- `Tests.Components` — the dialog shows once, dismisses, and stays away once acknowledged.

## 12. Documentation to update in the same PR

| Doc | Change |
|---|---|
| `docs/SCHEDULED-JOBS.md` | delete the `flush-overdue-score-batches` row |
| `docs/DATABASE-SCHEMA.md` | `ScoreSession.ProcessedAt`, `ImportResult.AcknowledgedAt` |
| `docs/API.md` | duplicate-delivery note beside the webhook payload |
| `docs/design/import-completeness-check.md` §373 | the `ScoreCount` 0-forever claim — the replay now sets it |
| `docs/design/session-breakdown.md` | pointer to this doc from the `ScoreBatchPolicy` note (D39a) |
| `docs/design/import-scores-refresh.md` | the import strip gains the `Interrupted` state |
| `IScoreJournalRepository.GetChartHistories` XML doc | **code, not a doc, but same class of fix** — it still claims chart ids are mix-scoped, which the implementation contradicts (§5) |

## 13. Technical scope

File-level, current as of the verification pass on 2026-08-09. One PR.

### ScoreTracker.ScoreLedger

| | File | Note |
|---|---|---|
| new | `Domain/SessionReplayBuilder.cs` | the pure function (§5), including the mix filter |
| new | `Contracts/Queries/GetUnprocessedSessionsQuery.cs` | what the recovery pass reads first (§3.1) |
| new | `Contracts/Commands/ReplaySessionCommand.cs` | |
| new | `Application/SessionRecoverySaga.cs` | the query handler, the replay handler, and the `ScoreHighlightsCapturedEvent` consumer that stamps the marker |
| edit | `Infrastructure/Entities/ScoreSessionEntity.cs` | `ProcessedAt` |
| edit | `Domain/IScoreSessionRepository.cs` | `ListUnprocessed`, `MarkProcessed`, and a **set**-counts method (§5.2) |
| edit | `Infrastructure/EFScoreSessionRepository.cs` | those three |
| edit | `Domain/IScoreJournalRepository.cs` | the stale cross-mix XML doc (§5) |
| edit | `Application/UpdatePhoenixRecordHandler.cs` | drop the flush consumer; make `PublishScoreEvents` reachable by the replay |
| edit | `Contracts/ScoreBatchPolicy.cs` | correct the Hangfire-safety-net claim (§1.1) |
| edit | `Wiring/ScoreLedgerRegistrationExtensions.cs` | register the new consumer — the hook holds 3 today |
| edit | `Wiring/ScoreLedgerModelContribution.cs` | filtered `ProcessedAt` index (§5.3) |
| edit | `ScoreTracker.ScoreLedger.csproj` | direct `PlayerProgress` reference (§4.1) |
| delete | `Contracts/Messages/FlushOverdueScoreBatchesCommand.cs` | |

### ScoreTracker.OfficialMirror

| | File | Note |
|---|---|---|
| new | `Application/RecoverInterruptedImportsConsumer.cs` | the startup pass (§4) |
| new | `Contracts/Messages/RecoverInterruptedImportsCommand.cs` | |
| new | `Contracts/Queries/GetUnacknowledgedInterruptedImportQuery.cs` | what the dialog reads |
| new | `Contracts/Commands/AcknowledgeImportInterruptionCommand.cs` | |
| new | `Application/ImportInterruptionHandler.cs` | that query and command |
| edit | `Contracts/ImportOutcome.cs` | `Interrupted` (§7) |
| edit | `Domain/ImportFailureMessage.cs` | its copy |
| edit | `Infrastructure/Entities/ImportResultEntity.cs` | `AcknowledgedAt` |
| edit | `Domain/IImportResultRepository.cs` + `Infrastructure/EFImportResultRepository.cs` | lookup by session ids, acknowledge, newest-unacknowledged |
| edit | `Wiring/OfficialMirrorRegistrationExtensions.cs` | register the consumer |
| edit | `Wiring/OfficialMirrorModelContribution.cs` | `IX_ImportResult_SessionId` (§5.3) |

### ScoreTracker (Web)

| | File | Note |
|---|---|---|
| new | `HostedServices/StartupRecoveryPublisher.cs` | publishes one message in `StartAsync`. The whole trigger. |
| new | `Components/InterruptedImportPointer.razor` | modelled on `DeletionNoticeHost` (§7) |
| edit | `Shared/MainLayout.razor` | a fourth island line beside the existing three |
| edit | `Components/ImportResultStrip.razor` | the `Interrupted` state |
| edit | `Components/CommunityTools/ToolWebhookPanel.razor` | the duplicate-delivery alert (§8) |
| edit | `Program.cs` | register the hosted service; **delete** the `flush-overdue-score-batches` cron line |
| edit | `HostedServices/RecurringJobRunner.cs` | **delete** `PublishFlushOverdueScoreBatches` |
| edit | 9 × `Resources/App.*.resx` | one pass, alphabetical insertion, en-ZW included |

### ScoreTracker.PlayerProgress / ScoreTracker.CommunityTools

Nothing. PlayerProgress cannot reach either vertical (§4.1) and needs no change; CommunityTools'
behaviour is unchanged and only the Web-side panel copy moves.

### ScoreTracker.Data

One migration: `ScoreSession.ProcessedAt` **plus its backfill**, `ImportResult.AcknowledgedAt`, and
the two indexes from §5.3.

### Tests

| | File | Note |
|---|---|---|
| new | `Tests/DomainTests/SessionReplayBuilderTests.cs` | every §5 rule, cross-mix case included |
| new | `Tests/ApplicationTests/RecoverInterruptedImportsConsumerTests.cs` | candidate selection, the four run states |
| new | `Tests/ApplicationTests/SessionRecoverySagaTests.cs` | replay, stamp, already-processed no-op |
| edit | `Tests/ApplicationTests/UpdatePhoenixRecordHandlerTests.cs` | delete the four flush tests |
| edit | `Tests/ApplicationTests/HighlightCaptureSagaTests.cs` | assert the event publishes on the zero-flags path — the stamp depends on it |
| edit | `Tests/ArchitectureTests/VerticalBoundaryTests.cs` | assert both new consumers resolve |
| new | `Tests.Integration/` | the backfill leaves nothing unprocessed; a replay **sets** counts rather than adding |
| new | `Tests.Components/` | the dialog shows once and stays away once acknowledged; the strip's `Interrupted` state |

### Size

~57 files, ~800–1,000 LOC net new, ~150 deleted, one migration. Most of the file count is the nine
resx files and one-line wiring edits.

## 14. Build order

One PR, six commits:

1. This document.
2. **Delete `flush-overdue-score-batches`** — message, consumer, runner method, cron line, its four
   tests, the `SCHEDULED-JOBS.md` row, and the `ScoreBatchPolicy` comment. `Dump()` and
   `BatchAccumulatorSnapshotEntry` **stay** — `AdminController.GetScoreBatches` still uses them.
3. **The replay mechanism, dark** — `SessionReplayBuilder` and its tests, `ProcessedAt` and the
   migration, `ReplaySessionCommand`, `GetUnprocessedSessionsQuery`, the stamp consumer, the
   `PlayerProgress` reference. Nothing calls the replay yet.
4. **The recovery pass turns it on** — the OfficialMirror consumer, `Interrupted`, `AcknowledgedAt`,
   the hosted service, the strip's new state, and **all** new locale keys in one pass.
5. **The dialog** — the island and its component tests. Pure Razor; commit 4 seeded the keys.
6. **The webhook disclaimer** — the panel alert and the `docs/API.md` line.
