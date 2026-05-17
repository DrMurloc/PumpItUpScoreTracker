# Feature: Notifications gating (CommunitySaga, Discord)

> Status: **design locked** · Last updated: 2026-05-16

Community side-effects — Discord notifications, community leaderboards — fire only when the score's mix matches the live mix.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`mix-model.md`](mix-model.md)
- [`events.md`](events.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Eventing"

## Scope

- The rule for when Community/Discord side-effects fire
- Where the rule is enforced
- How it behaves during the LiveMix transition

## Out of scope

- Adding `Mix` to events themselves — see [events.md](events.md)
- The community leaderboard data model — see [derived-data.md](derived-data.md)
- Future support for old-mix submissions being notification-worthy — explicitly deferred per workshop

## Locked decisions

- **The rule** — Discord and community-side notification consumers fire **only when `event.Mix == liveMixAccessor.GetLiveMix()`**. Any score event for a non-live mix (Phoenix 1 scores after Phoenix 2 goes live, future XX manual entries, future older-mix support) is silently dropped at the consumer level.
- Future "support old-mix notifications" work is tackled as a feature add later if/when scope opens. Not in Phoenix 2 release scope.

## Why this matters

Two failure modes the gating prevents:

1. **Discord notification storm during transition.** A user importing Phoenix 1 scores after Phoenix 2 is live shouldn't blast Phoenix 1 PB announcements into Discord channels that are now Phoenix-2-focused.
2. **Community leaderboard pollution.** Phoenix 2 community leaderboards shouldn't include Phoenix 1 scores from late-importing users.

The rule is mix-aware **without** requiring complex per-channel routing — the community side just stops listening to non-live-mix events.

## Implementation shape

Each affected consumer adds a guard at the top of `Consume`:

```csharp
public async Task Consume(ConsumeContext<PlayerScoreUpdatedEvent> context)
{
    if (context.Message.Mix != _currentUserAccessor.GetLiveMix())
        return;

    // ...existing community/Discord logic...
}
```

Or, cleaner, a small extension method:

```csharp
public static bool IsLiveMixEvent(this ConsumeContext<TEvent> ctx, ILiveMixAccessor liveMix)
    where TEvent : IMixedEvent
    => ctx.Message.Mix == liveMix.GetLiveMix();
```

(Worth considering an `IMixedEvent` marker interface during the events.md work — it'd make this guard generic across all mixed events.)

## Consumers that need the guard

Audit during implementation. Starting list (from the explore pass):

- `CommunitySaga` — consumes `RecentScoreImportedEvent`, `NewTitlesAcquiredEvent`, `TitlesDetectedEvent`. All three need gating.
- `QualifiersSaga` — note: Qualifiers carry their own mix on the Qualifier config (see [qualifiers-mom.md](qualifiers-mom.md)). The gating here is "this score event's mix matches the Qualifier's configured mix," not the live mix. Different rule, same shape.
- Any Discord-publishing handler in `ScoreTracker.Application/Handlers/`

## Files this touches

- `ScoreTracker.Application/Handlers/CommunitySaga.cs`
- Any other consumer that publishes Discord notifications or updates community-visible state
- Possibly: new `ScoreTracker.Domain/Events/IMixedEvent.cs` marker interface

## Risks

- **Forgetting a consumer.** Any consumer that should be gated but isn't will leak Phoenix 1 notifications after the LiveMix flip. The mitigation is mechanical: grep for `IConsumer<` in `ScoreTracker.Application/Handlers/` and `ScoreTracker.PersonalProgress/`, audit each for whether it produces user-visible community side-effects.
- **Test coverage gap.** Worth a component test per affected saga asserting "given event.Mix != LiveMix, no Discord call is made." Mock the live mix accessor, mock `IBotClient`, verify no calls.

## Open questions

None known.

## Changelog

- 2026-05-16: Doc created from workshop.
