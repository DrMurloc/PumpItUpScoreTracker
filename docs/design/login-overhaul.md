# Login overhaul — design

Multi-provider sign-in, PIUGAME as a login source, and account merging. This doc records the
design and the decisions behind it; the commit-by-commit build plan is
[login-overhaul-spec.md](login-overhaul-spec.md).

## Goals

1. All existing logins keep working unchanged through the rollout.
2. Sign-in methods become many-to-one with accounts (one account can have Google + Discord + …).
3. Users can merge two accounts: one account survives with all sign-in methods; the other's data
   is deleted. The user chooses which, with data shown to inform the choice.
4. "Log in with PIUGAME": a username/password screen that authenticates against piugame.com's
   login. We never store the password and it must never be logged.
5. When a login or score import surfaces a game tag that already exists here, offer a merge.
6. Merging requires proving control of **both** accounts (one successful sign-in per account —
   not every method on every account).

## Current state (what makes this cheap)

- `ExternalLoginEntity` already keys on `(LoginProvider, ExternalId)` with a non-unique `UserId`
  FK — the schema is **already many-to-one**. Requirement 2 is flow work, not a migration.
- `PiuGameApi.GetSessionId` already performs the piugame credential login (`login_check.php`,
  `sid` cookie, am-pass SSO-bounce retry). A successful session **is** the authentication.
- Credentials are transient today (import page → OfficialMirror queries → HTTP client); nothing
  persists them. There is no MediatR/MassTransit pipeline that logs request objects.
- There is **no delete-account feature**, but there is a self-serve "Delete All Scores" button
  (`WipeUserScoresCommand` on ScoreLedger's contracts: Phoenix records, XX attempts, player
  stats, highest title, optional history). The merge purge reuses it for the score-data portion;
  the remaining per-vertical deletes are net-new and double as real account deletion later.

## The mental model

One **account** (game tag, scores, titles, communities); many **sign-in methods**. The Account
page gets a "Sign-in methods" panel — the GitHub/Steam "connected accounts" pattern — listing
Discord / Google / Facebook / PIUGAME with connect and disconnect actions. Vocabulary discipline
everywhere: we never say "accounts" for login sources.

Guards: you cannot disconnect your last sign-in method (lockout prevention).

## PIUGAME as a sign-in provider

A first-class "Log in with PIUGAME" button on the login page (it is the lowest-friction door for
the Phoenix 2 wave), leading to a username/password form on our site.

- **Trust copy**: the form states prominently that credentials are sent directly to piugame and
  never stored. (Same posture as the existing import page.)
- **Identity, not just session**: after `GetSessionId` succeeds we scrape an identity bundle —
  the `mb_id` as typed, the main profile's `sub_profile` number, the set of game-card numbers,
  the game tag, the profile image.
- **ExternalId strategy — alias rows.** We do not yet know which piugame identifier is stable
  (`mb_id` may be the changeable am-pass credential; the display nickname definitely changes;
  the `sub_profile` numbers look like immutable server-side row ids — Andamiro question pending,
  see Open questions). So we don't bet on one: each identifier is stored as its **own
  `ExternalLogin` row** under provider `PiuGame`, namespaced in `ExternalId`
  (`mbid:<value>`, `profile:<no>`, `card:<no>`). Login matches on **any** alias and upserts the
  bundle on every success (self-healing). This reuses the existing table with zero migration —
  the many-to-one shape is exactly what alias rows need.
- **Drift degrades gracefully.** Unlike OAuth, piugame login re-proves the credentials every
  time, so stored aliases are only lookup keys, never trust anchors. If every alias drifts, the
  login looks new → a fresh account is created → the game-tag match fires → the merge wizard
  opens with the prove step already satisfied (see below) → two-click repair.
- **Availability coupling**: if piugame or the am-pass handshake is down, PIUGAME-only users
  can't sign in (the 30-day sliding cookie softens this). Mitigation: a persistent post-login
  nudge for PIUGAME-only accounts to add a backup sign-in method.
- Throttling against piugame is explicitly a non-concern (cleared with Andamiro directly).

### Password hygiene (requirement 4)

- The password travels **only** in POST bodies (to our server over the Blazor circuit; to
  piugame in the `login_check.php` form). Never in a query string, route, or cookie.
- New contract records carry the password as a `RedactedString` value type whose `ToString()`
  returns `***` — C# records auto-generate `ToString()` including all members, so a plain
  `string Password` leaks the moment anyone logs the request object or embeds it in an
  exception. The three existing OfficialMirror credential queries get retrofitted.
- Tripwire test: piugame request construction never places `mb_password` in a URL, and
  credential-bearing records redact in `ToString()`.
- OTel/HTTP-client instrumentation records URIs, not bodies — kept that way.

## The merge wizard

**One wizard, three doorways.** Merge is a single flow (its own route under Account) entered
from:

1. **Link collision** — connecting a provider in settings finds that login on another account.
   The collision is the invitation, not an error.
2. **Game-tag match** — a PIUGAME login or score import returns a game tag that another account
   already has. Prompt: "An account with game tag X already exists. Is that you?" with a single
   CTA deep-linking into the wizard (returnUrl back to finish the import afterwards). We do
   **not** build a second inline resolution flow — one wizard, one code path.
3. **Manual** — a "Merge another account" button in settings.

The wizard is **prove → choose → confirm**:

- **Prove.** You're signed into account A; complete one sign-in belonging to account B in
  *verify mode* — an OAuth challenge or piugame credential check that does not switch your
  session. One proof per account (requirement 6). Successful authentications anywhere in the
  app (login, link, import) are recorded as short-lived **verified-login tokens**
  (provider + external id, ~10 min TTL, server-side). The wizard consumes them — so after a
  piugame login that triggered a tag match against a PIUGAME-sourced account, the prove step is
  already satisfied and the user lands straight on the compare screen.
- **Choose.** Side-by-side comparison of the two accounts: member since, Phoenix/XX score
  counts, oldest and newest score, Pumbility, titles, communities. A soft "most history" hint
  when it's lopsided, never auto-selected. Framing is **"which account do you keep"** — same
  decision as "which is deleted," much less frightening, and it makes keep-the-account and
  keep-the-data visibly one choice.
- **Confirm.** Spell out exactly what is deleted ("2,431 scores, 12 titles…"), require typing
  the game tag, and state the reassurance that matters: *all sign-in methods end up on the
  surviving account* — however you signed in yesterday still works tomorrow.

### Why game-tag matching is safe

Game tags are self-reported and non-unique, so a tag match is only ever an *invitation*. The
prove step gates the actual merge behind demonstrating control of both accounts, so a false or
malicious match can't take anything over.

## Merge execution and the grace period

Merging is destructive, so it is **soft-first** (this flow is the future #1 support-ticket
generator):

- **At merge time** (single Identity-owned transaction + event): the retired account's
  `ExternalLogin` rows re-point to the survivor (the moved set is recorded for undo); the
  retired `User` is hidden (`IsPublic = false`, game tag cleared) but its data is untouched;
  both accounts' claims are invalidated via the existing `ClaimsInvalidatedAt` mechanism;
  a durable `MergeRequest` row records survivor, retired user, moved logins, and
  `PurgeAfter = now + 30 days`; Identity publishes `AccountsMergedEvent` (past-tense fact).
- **Undo** within the grace window (self-serve from the survivor, or admin): move the recorded
  logins back, un-hide the retired user, cancel the purge.
- **Purge** after 30 days: a Hangfire recurring job publishes a trigger; Identity finds due
  merges and publishes a per-user purge event; **each vertical owns deleting its own rows**
  via an idempotent bus consumer (cross-vertical SQL stays forbidden — user data spans ~20
  tables across every vertical). Score data rides the existing `WipeUserScoresCommand`;
  Identity deletes its own rows and the `User` last.
- **Crash safety**: the bus is in-memory, so a process death mid-purge loses in-flight
  consumers. The purge trigger therefore **re-fires daily for a week** past `PurgeAfter`;
  deletes are naturally idempotent, so re-firing is free and self-healing.

The same purge event chain is the future self-serve delete-account feature — merge is "purge +
move logins," delete is just "purge."

## Rollout order

All changes are additive (requirement 1). Three independently shippable phases:

1. **Sign-in methods panel** — list / connect / disconnect, link-mode OAuth callback.
2. **PIUGAME login** — provider, login form, alias matching, hygiene, backup-method nudge.
3. **Merge** — verified-login tokens, MergeRequest, wizard, entry points, purge job.

## Open questions

- **Stable piugame identifier**: ask Andamiro whether a stable member number exists and whether
  the login id survives email/nickname changes. The alias-row design works regardless; the
  answer just decides which alias we display and trust most.
- **Retired-account visibility during grace**: hiding via `IsPublic = false` + cleared game tag
  covers the main surfaces; any leaderboard that ignores those flags may show a ghost for up to
  30 days. Acceptable, revisit if reported.
