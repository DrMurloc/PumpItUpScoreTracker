# Login overhaul — technical gameplan

Commit-by-commit build plan for [login-overhaul.md](login-overhaul.md). Three phases, each
independently shippable. Every commit builds green, passes the architecture ratchets, and
updates the affected reader docs in the same PR. No new NuGet packages are needed anywhere in
this plan.

Conventions that apply to every commit and aren't repeated below:

- New commands/queries/events/DTO records get `[ExcludeFromCodeCoverage]` and follow the message
  taxonomy (folder + name + interface).
- New UI strings get localization keys populated in **all eight locales** in the same commit.
- Handler tests follow the canonical `ApplicationTests` mock-port pattern; `IBus` publishes are
  `Verify`'d.

---

## Phase A — sign-in methods panel

### A1 — Identity plumbing: list and remove external logins

- **Scope**: `GetExternalLoginsQuery(UserId)` → list of `(LoginProviderName, ExternalId)` in
  `ScoreTracker.Identity/Contracts/Queries/`; `RemoveExternalLoginCommand(LoginProviderName,
  ExternalId)` (operates on the current user) in `Contracts/Commands/`; handlers in the
  vertical's `Application/`. The remove handler enforces the **last-login guard** — removing the
  only remaining sign-in method throws a domain exception.
- **Ports**: extend `IUserRepository` (`Domain.SecondaryPorts`) with `GetExternalLogins(userId)`
  and `RemoveExternalLogin(userId, provider, externalId)`; implement in `EFUserRepository`
  (no schema change — `ExternalLogin` is already many-to-one).
- **Tests**: handler tests (stubbed `IUserRepository`; guard case asserts the throw and verifies
  no repository delete).

### A2 — Link-mode OAuth challenge

- **Scope**: `LoginController` gains `GET /Login/{provider}/Link` (authorize-gated) issuing the
  OAuth challenge with callback `GET /Login/{provider}/Link/Callback`. Callback outcomes:
  externalId unused → `CreateExternalLoginCommand`, back to Account with success; already on the
  current account → no-op notice; **on another account → notice naming the conflict** (upgraded
  into the merge invitation in C6). No claims changes; session untouched.
- **Tests**: none beyond build (thin controller, mirrors the existing callback); manual
  verification against DevAuth is noted in the PR.

### A3 — "Sign-in methods" panel on the Account page

- **Scope**: new section on `Pages/Account.razor`: current methods via
  `GetExternalLoginsQuery`, per-provider connect buttons (→ `/Login/{provider}/Link`),
  disconnect with confirmation dialog, disabled-with-explanation when it's the last method.
  Vocabulary: "sign-in methods", never "accounts".
- **Docs**: ARCHITECTURE.md login-flow paragraph mentions linking.

**Phase A ships.**

---

## Phase B — PIUGAME login

### B1 — `RedactedString` + credential-logging ratchet

- **Scope**: `RedactedString` value type (`From` factory, implicit conversions, `ToString()` →
  `"***"`) — placed with the shared value types. Retrofit the three existing credential-bearing
  OfficialMirror queries (`GetOfficialRecentScoresQuery`, `GetOfficialAccountDataQuery`,
  `GetGameCardsQuery`) and audit any import-saga message carrying credentials. Records
  auto-generate `ToString()` over all members, so a raw `string Password` in a request record
  leaks the moment anyone logs it.
- **Tests**: `DomainTests` for redaction; **new architecture ratchet**: no command/query/message
  record may declare a `string` property whose name contains `Password` — must be
  `RedactedString`.

### B2 — OfficialMirror identity contract

- **Scope**: `GetPiuGameAccountIdentityQuery(Username, RedactedString Password)` →
  `PiuGameAccountIdentity { MbId, MainProfileNo, CardNos, GameTag, ProfileImage }` in
  `ScoreTracker.OfficialMirror/Contracts/Queries/`. Handler composes the existing
  `GetSessionId` + `GetCards` + `GetAccountData`. Invalid credentials surface as the existing
  `InvalidCredentialException`; the am-pass bounce retry is already inside `PiuGameApi`.
- **Tests**: handler test with a stubbed site-client seam, mirroring existing OfficialMirror
  handler tests; tripwire that `mb_password` never appears in a request URL.

### B3 — PIUGAME login flow

- **Scope**:
  - `/Login/PiuGame` Razor page: username/password form (POST over the circuit only), the
    "sent directly to piugame.com — never stored" copy, and a first-class button on `/Login`.
  - On success: match **any alias** — new `IUserRepository.GetUserByAnyExternalLogin(provider,
    externalIds)`; aliases stored as namespaced `ExternalLogin` rows under provider `PiuGame`
    (`mbid:<value>`, `profile:<no>`, `card:<no>`), upserted on every successful login
    (self-healing). No match → create user (name/game tag/profile image from the identity
    bundle) + alias rows → `/Welcome`.
  - Cookie issuance reuses the existing callback path (`ICurrentUserAccessor.SetCurrentUser`).
- **Tests**: handler-level tests for alias match/upsert/create paths.
- **Docs**: ARCHITECTURE.md login-flow section (provider list + piugame path).

### B4 — Attach-PIUGAME from settings + backup-method nudge

- **Scope**: the A3 panel's PIUGAME connect opens the same credential form in attach mode
  (aliases land on the *current* account; collision with another account → notice, upgraded in
  C6). Post-login nudge for accounts whose only provider is `PiuGame`: "Add a backup sign-in
  method" banner linking to the panel.

**Phase B ships.** (PIUGAME import UX on `UploadPhoenixScores` is untouched — see "Considered
and dropped" below.)

---

## Phase C — account merge

### C1 — Verified-login tokens

- **Scope**: a circuit-scoped Web service recording `(provider, externalId, verifiedAt)` on
  every successful authentication: OAuth login callback, link callback (A2), PIUGAME login and
  attach (B3/B4), and score import. TTL ~10 minutes via `IDateTimeOffsetAccessor`. Pure class +
  DI registration; this is the merge wizard's proof source.
- **Tests**: unit tests with `FakeDateTime` for TTL expiry.

### C2 — `MergeRequest` table

- **Scope**: Identity-owned entity (`Identity/Infrastructure/Entities/`, registered through the
  vertical's `IDbModelContribution`): survivor + retired user ids, moved-logins JSON (for
  undo), state (`Active` / `Undone` / `PurgeDue` / `Purged`), `CreatedAt`, `PurgeAfter`,
  `PurgedAt`. Migration scaffolded from `ScoreTracker.Data` with
  `--startup-project ../ScoreTracker.CompositionRoot`.
- **Docs**: DATABASE-SCHEMA.md row.

### C3 — Merge execution + undo

- **Scope**: in Identity `Contracts/Commands/`: `ExecuteMergeCommand(SurvivorUserId,
  RetiredUserId)` — guards (distinct users, both exist, neither already retired), re-points the
  retired account's `ExternalLogin` rows to the survivor, hides the retired user
  (`IsPublic = false`, game tag cleared), invalidates claims on **both** users, writes the
  `MergeRequest` (`PurgeAfter = now + 30d`), publishes `AccountsMergedEvent(SurvivorUserId,
  RetiredUserId)` from `Contracts/Events/`. `UndoMergeCommand(MergeRequestId)` restores the
  recorded logins, un-hides the user, sets `Undone`.
- **Tests**: handler tests; `IBus.Publish` verified; undo restores exactly the moved set.

### C4 — Compare-screen data

- **Scope**: minimal summary queries on the owning verticals' contracts where none exist —
  ScoreLedger (Phoenix/XX score counts, oldest/newest score timestamps from the journal),
  PlayerProgress (Pumbility, title count — reuse existing stats queries where possible),
  Communities (membership count). The wizard page composes these via `IMediator`; **no
  cross-vertical aggregator handler** and no SQL joins.
- **Tests**: per-vertical handler tests for the new queries.

### C5 — The merge wizard

- **Scope**: `/Account/Merge` (authorize-gated), prove → choose → confirm:
  - **Prove**: shows the other account's providers; satisfied instantly if C1 holds a fresh
    token for any of them, else offers the OAuth *verify-mode* challenge
    (`/Login/{provider}/Verify` — authenticates without switching the session, records a token)
    or the PIUGAME credential form.
  - **Choose**: side-by-side cards from C4 (member since, scores, oldest/newest, Pumbility,
    titles, communities), "most history" hint, framed as *which account to keep*.
  - **Confirm**: itemized deletion summary, type-the-game-tag confirmation, the "all sign-in
    methods keep working" reassurance, then `ExecuteMergeCommand` and re-sign-in as survivor.
  - Deep-linkable with a target-account context parameter and `returnUrl` (for C6 doorways).
- **Docs**: ARCHITECTURE.md pages table row.

### C6 — The three doorways

- **Scope**:
  1. **Link collision** (A2/B4 notices) becomes the invitation: "This login belongs to
     account X (N scores). Merge?" → deep link.
  2. **Game-tag match**: `GetUsersByGameTagQuery` (Identity contracts + `IUserRepository`
     method); after a PIUGAME login or import completes, a match on another account prompts
     "An account with game tag X already exists. Is that you?" → deep link, `returnUrl` back to
     the import. Invitation only — the wizard's prove step is the security gate.
  3. **Manual**: "Merge another account" button in the settings panel.
- **Tests**: query handler test; prompt logic covered at handler level.

### C7 — Purge pipeline

- **Scope**:
  - Trigger message `ProcessAccountPurgesCommand` (plain record, `Application/Messages/`),
    published by a new one-line `RecurringJobRunner` method; `RecurringJob.AddOrUpdate` in
    `Program.cs` (daily, UTC).
  - Identity consumer: for each `MergeRequest` past `PurgeAfter` and not `Purged`, publish
    `AccountPurgeStartedEvent(RetiredUserId)` (Identity `Contracts/Events/`); mark `Purged`
    once `PurgeAfter + 7d` has passed. **Re-firing daily for that week is the crash-safety
    mechanism** — the in-memory bus loses in-flight consumers on process death, and idempotent
    deletes make re-delivery free.
  - Each vertical adds an idempotent `IConsumer<AccountPurgeStartedEvent>` deleting its own
    user-keyed rows, registered through its `AddXxxConsumers` hook (tripwire tests cover
    discovery). ScoreLedger/PlayerProgress score data reuses the existing
    `WipeUserScoresCommand(userId, IncludeHistory: true)` (already tested, already an allowed
    arch-test exception for its cross-port reach). Identity deletes its own rows and the
    `User` row last.
- **Tests**: consumer tests per vertical; one `Tests.Integration` case seeding representative
  rows across verticals, running the purge, asserting the wipe (Testcontainers + Respawn).
- **Docs**: SCHEDULED-JOBS.md row.

### C8 (optional follow-up) — self-serve account deletion

- **Scope**: `DeleteAccountCommand` = C3's hide/claims-invalidate + a `MergeRequest`-style purge
  record without login moves, riding the C7 chain unchanged. Extends the existing "Delete All
  Scores" self-service to the whole account and replaces the manual process the privacy policy
  describes.

---

## Considered and dropped — PIUGAME sign-in powering the import

Session reuse ("you just logged in with these credentials, skip retyping them on the import
page") and login-triggered auto-import were considered and **dropped** (owner call,
2026-07-03): sign-ins are rare thanks to the 30-day sliding cookie — far rarer than the
piugame session lifetime — so the dominant flow is *go to site → import*, not *log in →
import*. The seam the feature would smooth is one almost nobody crosses. The import page keeps
its credential form as-is.

## Sequencing notes

- A and B are independent of C but C assumes both (verify-mode challenges reuse A2's link
  plumbing; PIUGAME proofs reuse B3's form).
- The Andamiro stable-identifier answer can land any time — it only changes which alias is
  displayed as primary (B3), never the matching logic.
- Public `api/*` wire shapes are untouched throughout; `Tests.Api` should stay green with zero
  edits. Any failure there means a commit leaked into the public contract and needs review.
