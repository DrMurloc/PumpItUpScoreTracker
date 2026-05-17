# Feature: Qualifiers and March of Murlocs

> Status: **design locked** · Last updated: 2026-05-16

The two tournament-shaped features that survive into Phoenix 2. Both are mix-aware; everything else in the tournament domain is being dropped.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Layers / Application", subsection on Saga conventions

## Scope

- `TournamentSession.Mix` — set-once at session creation
- Qualifiers configuration carrying `Mix` for the duration of the Qualifier
- "No mixed-mix sessions" invariant
- The dropped tournament features (so future-you doesn't go looking)

## Out of scope

- Match-shaped tournament features (bracket play, etc.) — being **dropped** going into Phoenix 2. No mix work needed because the feature is going away.
- Pumbility/PUMBILITY+ algorithm changes — handled per existing service, just mix-parameterized.

## Locked decisions

- **F1** — `MixId` lives on `TournamentSession` (the user's registration in a tournament), **not** on `TournamentEntity` (the tournament itself). A tournament can host sessions of any mix; each session picks one.
- **F2** — Qualifiers carry their `Mix` on the Qualifier configuration. All Qualifier actions (score submissions, leaderboard reads, eligibility checks) thread that Mix through.
- **F3** — `TournamentSession.Mix` is **set-once-at-creation and immutable**. Once a session exists with a mix, that mix is locked. Matches the existing `TournamentSession` invariant style (`Approve`, `AddPhoto`, etc. are monotonic).

## Why `Mix` is on the session, not the tournament

The roadmap requirement: "March of Murlocs should support any mix, but you must select Mix when registering a session (I.E. no mixed-mix sessions)."

That's a session-level rule, not a tournament-level rule. If `Mix` were on the tournament, the tournament would have to be Phoenix-1-only or Phoenix-2-only — and you'd need separate M.o.M. tournaments per mix. Putting `Mix` on the session lets one M.o.M. event accept both mixes of player, with each session sandboxed to its declared mix.

## TournamentSession shape

```csharp
// ScoreTracker.Domain/Models/TournamentSession.cs
public sealed record TournamentSession
{
    // ...existing properties...

    public MixEnum Mix { get; }  // set in the constructor, no setter, no With() that changes it

    public TournamentSession(
        // ...existing args...
        MixEnum mix)
    {
        // ...
        Mix = mix;
    }

    // No method on TournamentSession may mutate Mix. If a future feature wants
    // "change session mix," that's a new TournamentSession.
}
```

Approval, photo-add, etc. remain unchanged. They all operate within the session's declared mix.

## Qualifiers shape

The Qualifier configuration (the entity that defines a Qualifier event) gains a `MixEnum Mix` property. Every Qualifier-shaped query/command takes a Qualifier ID, and from that resolves the mix — no separate mix parameter needed at the call site for most actions.

Submission flow (illustrative):

```csharp
// Application/Commands/SubmitQualifierScoreCommand.cs
public sealed record SubmitQualifierScoreCommand(
    Guid QualifierId,
    Guid ChartId,
    PhoenixScore Score)
    : IRequest;

// Handler
public async Task Handle(SubmitQualifierScoreCommand cmd, CancellationToken ct)
{
    var config = await _qualifierConfigRepo.Get(cmd.QualifierId, ct);
    // config.Mix is the authoritative mix for everything in this submission
    var score = new RecordedPhoenixScore(/* ... */, mix: config.Mix);
    await _scoreRepo.UpsertBestAttempt(score, config.Mix, ct);
    await _bus.Publish(new PlayerScoreUpdatedEvent { /* ... */, Mix = config.Mix });
}
```

The Qualifier config is the source of truth; everything downstream reads from it.

## Tournament features being dropped

These do **not** carry forward into Phoenix 2 and don't need mix support:

- Match-shaped tournament features (bracket play, head-to-head matches, `MatchSaga` operations)
- `MatchUpdatedEvent` and related
- Tournament leaderboards beyond M.o.M.'s own

The classes can remain in the codebase post-Phoenix 2 for historical Phoenix 1 data viewing, but new development against them stops. They are explicitly **not** in scope for the mix-threading pass — leaving them mix-blind is fine because Phoenix 2 won't use them.

## Files this touches

- Modified: `ScoreTracker.Domain/Models/TournamentSession.cs` — add `Mix` property, constructor argument, immutability
- Modified: `ScoreTracker.Data/Persistence/Entities/TournamentSessionEntity.cs` (or equivalent) — add `MixId` column
- Modified: Qualifier config entity and repository — add `MixId`, thread through queries/commands
- New EF migrations: add `MixId` to `TournamentSessions`, add `MixId` to Qualifier config table
- Backfill: existing rows → `Phoenix` mix ID

## Risks

- **Existing TournamentSession rows have no mix.** Backfill to `Phoenix`. Same migration pattern as PhoenixRecords.
- **Match-shaped features still exist in the codebase and quietly accept Phoenix-only data.** Document this explicitly so future-you doesn't try to retrofit mix support there in confusion — it's intentional that they remain mix-blind.
- **Qualifier mix mismatch across submission and chart.** If a Qualifier is configured for Phoenix 2 but accidentally references a chart that only exists in Phoenix 1, the composite FK on PhoenixRecords (`(ChartId, MixId)` → `ChartMix`) catches it. Good — no silent corruption.

## Open questions

- Is there a `TournamentSessionEntity` or similar EF entity, or is `TournamentSession` persisted via a different shape? Verify file naming during implementation. The principle holds either way.

## Changelog

- 2026-05-16: Doc created from workshop.
