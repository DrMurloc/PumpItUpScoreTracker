# Feature: Mix model and LiveMix

> Status: **design locked** · Last updated: 2026-05-16

The foundational mix model. Everything else in the Phoenix 2 rollout depends on this.

## Load these first (agent context)

- [`PHOENIX2-ROADMAP.md`](../../../PHOENIX2-ROADMAP.md)
- [`CLAUDE.md`](../../../CLAUDE.md)
- [`ARCHITECTURE.md`](../../../ARCHITECTURE.md) — section "Layers / Domain", section "Cross-cutting concerns"

## Scope

- The `MixEnum` shape and the `[LiveMix]` attribute
- How "the current live mix" is resolved at runtime
- How "this user's selected mix" is resolved at runtime
- UI selectability rule (which mixes appear in the dropdown)
- User-create writes `CurrentMix = LiveMix`
- Backfill for existing users with no persisted `Universal__CurrentMix`
- Anonymous user default behavior

## Out of scope

- Changes to PhoenixRecords schema — see [phoenix-records-schema.md](phoenix-records-schema.md)
- Threading Mix through queries/commands — see the per-feature docs and [phase-2-pre-launch.md](../phases/phase-2-pre-launch.md)

## Locked decisions

- **C1** — On user create, write `CurrentMix = LiveMix` to `UserUiSettings`. Existing users get backfilled. No read-time fallback for persisted users.
- **C2** — LiveMix is encoded as a `[LiveMix]` attribute on exactly one `MixEnum` value. Changing the live mix means moving the attribute and deploying. Mix changes are once-every-few-years, so a deploy is fine.
- **C3** — Existing users stay on their currently-selected mix (Phoenix 1) when Phoenix 2 goes live. They opt in by clicking. Auto-roll is rejected.
- **C4** — UI selectability rule: a mix is selectable iff it carries `[LiveMix]` OR is declared *before* the `[LiveMix]` value in `MixEnum` declaration order. Mixes declared after the live one are invisible until promoted.
- **H1** — Same `[LiveMix]` attribute serves as the toggle (no separate "enabled" flag). Phoenix 2 starts in the enum without the attribute → invisible. Move attribute → Phoenix 2 becomes live and selectable in one step.
- **A5** — Backfill `UserUiSettings.Universal__CurrentMix = Phoenix` for every existing user without a row, in the same migration that flips the create-handler to write `LiveMix`.

## The `MixEnum` shape

```csharp
// ScoreTracker.Domain/Enums/MixEnum.cs
public enum MixEnum
{
    XX,
    [LiveMix] Phoenix,
    Phoenix2     // declared but invisible until [LiveMix] moves
}

// ScoreTracker.Domain/Enums/LiveMixAttribute.cs (new)
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class LiveMixAttribute : Attribute { }
```

When Phoenix 2 launches, the attribute moves:

```csharp
public enum MixEnum
{
    XX,
    Phoenix,
    [LiveMix] Phoenix2
}
```

## Accessor shape

Two named methods, no silent fall-through:

```csharp
// ScoreTracker.Domain.SecondaryPorts (port)
public interface ICurrentUserAccessor
{
    // ...existing...
    MixEnum GetUserSelectedMix();    // throws if user is anonymous + no value persisted
    MixEnum GetLiveMix();             // resolves the [LiveMix]-attributed enum value
}
```

Implementation notes:

- `GetLiveMix()` reads the `MixEnum` once via reflection at startup and caches the value. Deploy = pickup.
- `GetUserSelectedMix()` for **persisted users** reads `UserUiSettings.Universal__CurrentMix` directly with no fallback. The backfill (A5) guarantees the value is present.
- For **anonymous users**, `GetUserSelectedMix()` reads from `ProtectedBrowserStorage` and falls back to `GetLiveMix()` if nothing is persisted. (This is the *only* fallback in the accessor surface; persisted users have no fallback.)
- Default in [`UiSettingsAccessor.cs:33`](../../../ScoreTracker/ScoreTracker/Services/UiSettingsAccessor.cs) is removed for the persisted path.

## UI selectability rule

In [`Shared/MainLayout.razor`](../../../ScoreTracker/ScoreTracker/Shared/MainLayout.razor), the mix dropdown enumerates `Enum.GetValues<MixEnum>()`. After this change, filter:

```csharp
private static readonly int LiveMixOrdinal = (int)typeof(MixEnum)
    .GetFields(BindingFlags.Public | BindingFlags.Static)
    .Single(f => f.GetCustomAttribute<LiveMixAttribute>() != null)
    .GetValue(null)!;

bool IsSelectable(MixEnum mix) => (int)mix <= LiveMixOrdinal;
```

`XX` (ordinal 0) is selectable. `Phoenix` (ordinal 1, [LiveMix]) is selectable. `Phoenix2` (ordinal 2) is hidden until the attribute moves.

## Backfill migration

Same PR that introduces the no-fallback accessor:

1. EF migration adds a backfill SQL statement: for every user without a `Universal__CurrentMix` row in `UserUiSettings`, insert one with value `Phoenix`.
2. Verify on a prod-sized restore before shipping.
3. Once shipped, the read path can safely assume the row exists for persisted users.

## Files this touches

- New: `ScoreTracker.Domain/Enums/LiveMixAttribute.cs`
- Modified: `ScoreTracker.Domain/Enums/MixEnum.cs` (add `Phoenix2`, add `[LiveMix]` on `Phoenix`)
- Modified: `ScoreTracker.Domain/SecondaryPorts/ICurrentUserAccessor.cs` (add `GetLiveMix()`, rename current method to `GetUserSelectedMix()` if the existing surface needs disambiguation)
- Modified: `ScoreTracker.Web/Accessors/HttpContextUserAccessor.cs`
- Modified: `ScoreTracker.Web/Services/UiSettingsAccessor.cs` (remove read-time default for persisted users; keep for anonymous)
- Modified: `ScoreTracker.Web/Shared/MainLayout.razor` (selectability filter)
- New EF migration in `ScoreTracker.Data/Migrations/` (backfill `Universal__CurrentMix`)

## Risks

- **Reflection cost on every accessor call** — mitigate by caching the resolved live mix at startup.
- **Backfill missing rows** — if any user has *some* UserUiSettings entries but not the mix one, the migration must `INSERT` for them, not assume one already exists. Verify with a per-user-count check.
- **Anonymous fallback feels asymmetric** — it is, by design. The asymmetry is documented; alternative (forcing anonymous users to pick a mix before any read) is worse UX.

## Open questions

None known. Ready to implement once Phase 2 begins.

## Changelog

- 2026-05-16: Doc created from workshop.
