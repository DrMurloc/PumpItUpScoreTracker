# Chart comments &amp; the details dialog overhaul

The chart details dialog becomes four tabs — **Comments, Leaderboard, Chart Stats, Score History** —
with the video staying above them. Comments are the feature; the rest is a reorganisation of what
the dialog already renders.

**[mock.html](mock.html) is the UI reference.** Open it before building any surface described here:
it carries the real Phoenix palette at true widths (572 px desktop, 390 px mobile) and shows every
state in one place — language badges, threads, the composer, the rules card, the link interstitial,
the report dialog, the moderation queue row.

> **Two parts of this folder are scaffolding and get deleted when the feature ships:**
> **`mock.html`**, and **Part 2 — Technical scope** below. What survives is Part 1, the feature
> requirements. Anything in Part 2 that turns out to be a durable architectural fact belongs in
> `ARCHITECTURE.md` or `CLAUDE.md` instead, moved there rather than left here.

---

# Part 1 — Feature requirements

---

## 1. The dialog

Nineteen call sites render `ChartDetailsDialog`. That number drives most of the structural calls.

**Header (outside the tabs):** video → title row (difficulty bubble, song name, To-Do) → your score
and standing. Three things you want without a click.

⚠ **The To-Do bookmark renders only when the host wires `OnToDo`** — ten of the thirteen hosts do
not, and before Slice 1 all of them showed a bookmark whose click invoked an unbound
`EventCallback` and silently did nothing.

**Tab strip is sticky** inside the scroll container, so a tall header scrolls away underneath it
instead of taking the tabs with it. On 390 px the strip **scrolls horizontally** rather than
truncating labels to jargon — the header alone is 274 px, so the fourth tab peeking is honest about
what is off-screen.

| Tab | Contents |
|---|---|
| **Comments** | Scope rail (Public · Notes · your communities), sort, threads, composer. |
| **Leaderboard** | `ChartLeaderboardScopes` unchanged — a move, not a rewrite. |
| **Chart Stats** | Meta grid, tier placements, skill bars, **similar charts**, PIU Center link. |
| **Score History** | The cross-mix journal, with **manual score edit at the bottom**. |

- **Every tab is lazy.** No queries until selected. The board already gates on `Active="Visible"`;
  the rest follow.
- **Default tab is Leaderboard**, last choice persisted in a `ChartDetails__Tab` UiSetting.
- **No count badge on the Comments tab.** It was the discovery mechanism in the first draft, and it
  costs a count query *on dialog open at every one of the nineteen call sites* — which is the one
  rule the tab strip exists to hold. Adding it later is one `COUNT` and one span, so it is a launch
  decision rather than a build one.
- **Score History hides when logged out. Comments does not** — it renders read-only, because a
  logged-out visitor arriving from a link is exactly who the conversation is for.
- **Recording a score is two taps**, and that is accepted (owner, field test). The pre-build
  assumption that several call sites "exist only to record" did not survive the audit:
  `QuickRecordWidget` has its own inline row and never renders this dialog at all, and
  `ChartSkills` — the one host wired to `OnScoreRecorded` — wants the board just as often. A
  header shortcut to Score History was built and then cut. **No host forces History.**
- **`InitialTab`** exists and is used by `MixChangesSongSheet`, which opens on Chart Stats: that
  sheet answers what changed about a chart, and the meta grid is the answer.
- **`FocusCommentId`** is the second new parameter, added in Slice 2: the moderation queue opens
  the dialog on Comments, anchored to the reported one.
- **Gone from the dialog in Slice 1**, both owner calls at field test: **Report Video** (the mail
  went unread; wrong videos arrive as Discord DMs, and no surface offers reporting now) and
  **Good Suggestion** (unused, and it wrapped the action bar onto two lines on a phone — took the
  `SuggestionCategory` parameter with it).

### Similar charts stays deliberately dumb

Stored match order from `GetSimilarChartsQuery` at the 0.55 floor. No re-sorting, no difficulty
lens, no filters — that machinery belongs to the chart page's shelf. A compact tile is the jacket
with the difficulty bubble overlaid bottom-right and no song name; tapping one **swaps the dialog
in place** via an internal chart override with a "← back to \<song\>" crumb, rather than pushing a
chart-change callback out to nineteen hosts.

**Six columns, dropping to three below 500 px.** Fixed columns rather than `auto-fill`: the jackets
are landscape, so their intrinsic width was deciding the track count and a 572 px dialog fitted
five. At six columns on a phone a 22 px bubble sat on a ~25 px-tall jacket and the tile was all
bubble. The bubble's own tooltip is suppressed (`DifficultyBubble.Tooltip="false"`, the opt-out
`ScoreBreakdown` already carried) because two fired on one tile.

The graph is populated — `recalculate-chart-similarity` is on a daily cron and has been running
since the chart-page overhaul deployed. Build the empty state anyway; it is what a chart the
piucenter crawl never covered shows, which is 139 of 4,426.

---

## 2. Comments

### The scope rail is the audience picker

Same `.cld-chip` vocabulary as the leaderboard's scopes, doing double duty: it filters what you read
*and* decides what you post to. "Which community am I posting to" is answered by where you are
standing, not by a second dropdown, and the composer states it in prose — *Posting to **Public** as
ERRLENA*, or on the Notes scope simply *Only you can see this*, because "as ERRLENA" means nothing
to an audience of one.

**Audiences are Public, your personal notes, and your non-regional communities**, in that rail
order. World and country boards are ownerless and carry no roles, so a comment posted there would
have no moderator and would fall to the site admin by default — the opposite of the deal in §4.
Notes sits second rather than last so it holds a stable position: it is the one chip every signed-in
player has, and it must not get pushed off the end by someone who joins six communities.

The list comes from `GetMyCommunitiesQuery`, filtered exactly the way `ChartLeaderboardScopes`
already filters it — `!IsRegional` **and** not the World community, which carries `IsRegional = 0`
and would otherwise mean everybody.

### Personal notes are an audience of one

Not a feature bolted beside comments: the same row with `Audience = Private`, which is what the rail
was already for. Same table, same aggregate, same parser, same link trust, same 500-character cap,
same purge manifest. Four invariants change:

- **Never translated.** Not "not yet" — never. §3's submit step skips `Private`, so a note costs
  nothing and no badge ever renders on one.
- **Never moderated.** No report, no shield, *and no admin visibility at all* — the site admin
  cannot read one. That is a query rule, not a hidden button.
- **Never voted, never replied to.** A root with no children, by construction. The Notes scope shows
  no sort control either; with no votes there is only one order.
- **No rules card.** The rules are about how you treat other people, so `AgreedToTermsAt` fires on
  your first **public or community** comment. Someone who only ever keeps notes is never asked to
  agree to anything.

Note rows drop the avatar column — every note is yours, so it is 44 px of nothing, and losing it
makes a note unmistakable for a comment at a glance. Links still autolink and still route through
the interstitial for an unknown host: the gate is about where a link goes, not who wrote it, and a
note is exactly where you paste a URL to your future self.

⚠ **Private notes share a table with public comments, so a wrong audience filter leaks them.** That
is not a new bug class — a community comment leaking to a non-member is the same mistake and exists
regardless — but it raises the stakes. The filter belongs in the repository rather than the UI, and
it wants a decoy-account integration test in the shape of `AccountPurgeTests`: a stranger holding a
note on every scope, which no other user's query may return under any sort, scope or moderator role.

### Threads

Root plus **one level of replies**; replying to a reply targets the root. A reply **inherits its
root's audience as a domain invariant on the aggregate**, not as a field the UI sets — you cannot
move a thread between communities or between community and public.

A removed comment leaves a stub (*Removed by a community admin · 4 days ago*) so the thread keeps
its shape.

### Votes

Thumbs up only, one per user, never on your own. Sort defaults to **Top** (votes desc, then newest)
with a **Newest** toggle.

### Editing and the three kinds of deletion

An edit writes a revision row, stamps `EditedAt`, and renders *(edited &lt;time&gt;)*. History is
retained for moderation.

Deletion is soft — `DeletedAt`, `DeletedByUserId`, reason — so the row stays queryable. **A deleted
comment leaves a stub only when a reply hangs off it**; otherwise the thread would fill with
headstones for comments nobody answered. Three actors, three stubs:

| Who | Row | Stub reads |
|---|---|---|
| The author | Soft-deleted | *Deleted · 4 days ago* |
| The site admin (Slice 2) or a community admin (Slice 3) | Soft-deleted, `DeletedByUserId` set | *Removed by the site admin · 4 days ago* |
| Account purge | **Tombstoned** — see below | *Comment from a deleted user* |

⚠ **Account purge cannot use the manifest for comments.** `UserDataPurge` issues a blanket
`DELETE … WHERE UserId = @id`, and deleting a root with replies orphans them — `AccountPurgeTests`
counts rows, so it would pass green while the tab throws. `CommentEntity` is therefore **exempt from
`UserOwned` with a reason**, exactly as `ToolEntity` already is, and ChartComments' purge repository
does the work itself before the row-level sweep:

1. **Roots that have replies are tombstoned**: `UserId` set to `Guid.Empty`, text cleared,
   `DeletedAt` stamped. The thread keeps its shape and the account keeps nothing.
2. **Everything else of theirs is hard-deleted** — leaf roots, replies, notes.
3. ⚠ **Revisions of a tombstoned comment are deleted too**, keyed by comment id rather than user id.
   They carry no `UserId` (which is why that table is separately exempt), so nothing else would
   reach them — and they hold the exact text the purge is supposed to remove.

The tombstone must not retain the purged `UserId`, or the decoy-account test correctly fails: a row
still keyed to a deleted account is a row the purge missed.

⚠ **An edit invalidates the translations** and re-queues the comment. The monthly ceiling is a fuse
against *bugs*; an edit loop is a **user-driven** cost amplifier it cannot see. A per-comment
re-translation cooldown is the mitigation, not a limit on editing.

### Plain text, and URLs autolink

**Stored as plain text. URLs autolink, newlines survive, runs of blank lines collapse to one, and
nothing else is interpreted.** No bold, italic, code or bullets; no headings, images, tables or raw
HTML. The 500-character cap counts what you typed, which is also what posts.

**A renderer is not optional; formatting is.** Autolinking *is* parsing — you have to find the URLs,
decide each one's trust inside the vertical, and emit nodes. So the parser lives in the vertical's
Domain and emits a token tree either way, Web renders tokens as real Blazor elements, and
`MarkupString` stays **structurally unreachable** rather than merely discouraged — precisely the
temptation the translation spec warns about ([comment-translation.md §5](../comment-translation.md)).
The parser is just ~60 lines instead of ~200.

Markdown was the first draft's answer, on the grounds that GitHub, Reddit and Discord ship it. Those
are long-form surfaces for people who write a lot. **YouTube, X, Instagram and Twitch all ship plain
text plus autolink** — short comments, phones, many languages, no power users — which is this
audience exactly. Four things follow:

- **Translation has nothing to corrupt.** The corpus measured `2:01` and blank lines surviving. It
  never measured `**drill**` going through Sonnet into Korean and back.
- **The link-set invariance check stays trivial** — set equality on bare URLs. Under markdown,
  `[label](url)` translates the label and not the target, so the one non-negotiable injection
  defence would have to reason about a pair instead of a set.
- **The cap stops taxing formatting.** `**drill**` spends 9 characters to show 5.
- **Six toolbar tooltips × nine locales stop existing**, along with the `wrapSelection` JS file and
  the Write/Preview toggle. The composer is one line that grows.

WYSIWYG stays rejected and got easier to reject: it was always additive rather than a replacement,
it stores HTML, and it now sits on top of a control one line tall. **Storing HTML is the door that
does not close again.**

⚠ **Plain text is slightly one-way.** Add markdown later and every existing comment containing `*`
or `_` reinterprets. Nothing like the HTML door — but if formatting is ever genuinely missed, the
cheap move is to interpret it only on comments written after the change, which the timestamp already
makes possible.

### The composer is one line

Plain text in a field that grows as you type, above the thread rather than below it — pinned to the
bottom would mean scrolling past twenty comments to write one, on a screen whose header is already
274 px. There is no button that opens a different control: the resting state *is* the field, and
**Reply** opens the identical line directly beneath the comment at the reply rail. One control,
three placeholders — *Add a comment…*, *Reply…*, *Add a note…*.

No character counter until you are near the cap, at which point it appears where the audience line
sits. **Twenty roots, then Show more** — replies hang off their root and do not count against it.
Replies are never collapsed: at this volume "1 reply ⌄" hides a conversation to save nothing.

### Link trust

Trusted hosts open straight through with `rel="noopener noreferrer nofollow ugc"`; everything else
gets an interstitial naming the host.

| Trusted | Matching |
|---|---|
| piuscores, youtube / youtu.be, reddit / redd.it, `pumpout2020.anyhowstep.com`, piugame, piucenter | **Dot-boundary suffix** — `host == d \|\| host.EndsWith("." + d)`, so `youtube.com.evil.tld` cannot slip through |
| Every **public** Community Tool's URL | **Exact host only** — a tool at `tools.example.com` must not bless `evil.example.com` |

- Only `http` and `https` parse. `javascript:` and `data:` are rejected at parse time.
- The interstitial shows the **parsed host**, never the link text, which is author-controlled.
- The tool list is data, so the allowlist is dynamic and memory-cached on a short TTL.
- ⚠ **Trust is resolved inside the vertical, not in Web.** Each link node in the token tree ships
  with its `IsTrusted` boolean already decided, so the presentation layer makes no policy call.

### Language

Everything in this subsection lands in **Slice 4**, with the pipeline. Slice 2 ships comments in the
language they were written in, with no badges and no reader-language control — which is honest, and
is the argument for Slice 4 better made than any annotation could.

⚠ **Source language is left null in Slice 2, not guessed.** Stamping the poster's UI culture looks
free and is not: a Korean player browsing in English gets recorded as `en-US`, and Slice 4 then
renders their comment Korean→Korean — precisely the rewrite `TranslationTarget.ForSource` exists to
prevent. The column ships in the Slice 2 migration so Slice 4 adds no schema; the pivot stage fills
it, having detected 34/34 across the corpus.

Five renderings: the **en-US pivot** plus **es-ES, fr-FR, ko-KR, pt-BR**. Everything else falls back
to English.

- ja-JP and it-IT are **deliberately out for launch** — a cost decision, revisited if volume proves
  cheap (§3).
- **es-MX** maps to es-ES. Mutually intelligible, the es-MX catalogue is contaminated, and the
  original is always available.
- **en-ZW (Murloc) is never a translation target.** Murloc readers get English.

Display resolution, in order:

1. Reader locale → mapped rendering locale.
2. Comment's source shares that language → show the **original**, no badge. Silence is the common case.
3. A rendering exists → show it, badged *Translated from 한국어*, with a per-comment **Show original**.
4. Not yet translated → show the **original** badged *Queued for translation*. The comment already
   exists in *some* language; an empty box is worse than the author's own words.

⚠ **Never render a comment back into its own language.** The round trip is a measured rewrite —
register raised, community vocabulary flattened, the most contemptuous phrase dropped, `1000%`
corrupted to `100%`. `TranslationTarget.ForSource` already encodes this; **absence of a key is the
instruction to show the original.**

An unsupported source language (Indonesian, say) is detected by stage one — 34/34 across the
corpus — and named for the reader via `CultureInfo.DisplayName`.

### The rules card

Shown before a first comment, following `ToolRulesCard` exactly: bold lead, body, checkbox, every
string through `L[…]`. Copy is in [mock.html](mock.html) §03.

⚠ **Rendered inline, in place of the composer** — not as a modal over the dialog. Tap the composer
the first time and the rules occupy it; Continue swaps them back. That is tidier, and it dodges a
trap this codebase has paid for twice: a `MudDialog` inside a `MudDialog`. A render-gated one orphans
its scrim and leaves the page dimmed and unclickable, and `charts.scss` line 1 forces
`max-width: none !important` on anything tagged `mud-dialog-width-sm`. Markup in the panel has no
scrim to orphan and no width to fight.

Two consents, collected when they become true:

| Record | Fires |
|---|---|
| `AgreedToTermsAt` + `TermsVersion` | Once, on the first **public or community** comment — never on a personal note. Versioned, so editing the rules re-prompts. |
| `ConsentedToPublicIdentityAt` | The first time a **private-profile** user posts to **Public**. |

Post to a community first and you see one checkbox; the identity checkbox appears later, when it is
actually true. These are real rows rather than UiSettings keys — an agreement wants a timestamp and
a version, and it should be auditable if a dispute lands.

⚠ A Murloc rendering exists purely to satisfy `LocalizationKeyTests` and
`MurlocValuesUseOnlyTheMurlocAlphabet`. Accepted risk, owner's call: someone browsing in Murloc may
accept text they cannot read.

---

## 3. Translation pipeline

**Sonnet 5, batched, pivot through English**, exactly as measured in
[comment-translation.md](../comment-translation.md). Nightly, oldest-first, with a **rolling monthly
$30 ceiling as the primary gate** and a nightly count (~50) underneath as smoothing.

### Cost

Derived from the workbench's measured $0.0211/comment by splitting on its stated ~60% input share:
~6,330 input + ~844 output tokens per comment. **Intro pricing ($2/$10 per MTok) ends 2026-08-31**;
everything below is standard $3/$15.

| Pipeline | Per comment, batched | 1,000/mo | 2,000/mo |
|---|---|---|---|
| **4 locales, as-built** ← shipping | $0.0158 | $15.83 | $31.66 |
| 6 locales (+ja +it), as-built | $0.0188 | $18.84 | $37.68 |
| 4 locales, glossary split | $0.0130 | $12.98 | $25.96 |
| 6 locales, glossary split | $0.0158 | $15.82 | $31.64 |

$30 buys **~1,900 comments/month** as shipped, against a realistic 100–300 steady state.

**The glossary split is deliberately not taken for launch.** It is the doc's own identified
optimisation (§3, ~30% input reduction) and it exactly pays for the two extra locales — but it means
re-tuning a prompt validated against a real corpus, and that sweep already showed a missing glossary
row producing `las tiradas` instead of `los runs`. It is the lever to pull when volume grows, by
which point there will be real data on which rows each stage uses.

### The batch state machine

The Batch API is asynchronous — most batches finish inside an hour, **maximum 24**, results retained
29 days, and **results return unordered, keyed by `custom_id`**. With two stages that is a four-state
machine per comment:

```
Pending → PivotSubmitted → PivotDone → FanOutSubmitted → Translated
                      ↘  Failed  ↙
```

Two recurring jobs: **Submit** (nightly — builds and submits both stages' batches) and **Collect**
(hourly — polls open batches, writes results, advances state, records usage).

- **`Audience = Private` is never submitted.** A personal note has an audience of one who already
  reads the language it was written in. This is a permanent exclusion in the submit step, not a
  deferral.
- The **ceiling is checked at submit time**, against rolling 30-day actual usage from completed
  batches plus an estimate for in-flight ones. Spending is not something to discover afterwards.
- A `translated_by` provenance column on every rendering — model and path. It is what makes "why do
  these two hundred read differently?" answerable.
- ⚠ **A rendering whose link set differs from the source's is rejected.** Required by
  [comment-translation.md §5](../comment-translation.md) and non-negotiable: it is the deterministic,
  free defence against the only prompt injection worth an attacker's effort.
- Re-translation after a prompt or glossary change is **admin-triggered only**, and quotes its cost
  first.
- Admin page: backlog depth, oldest pending age, rolling spend against the ceiling, last run,
  failures, **Drain now**.

⚠ **This retires a documented safety property.** Today `ILanguageModelClient` has no implementation
in `Data` and no DI registration, so no shipping code path can spend a token. This feature ends that
on purpose. The ceiling, the nightly count and the submit-time check are what replace it.

---

## 4. Moderation

The deal, in the owner's words: **communities moderate themselves; public comments are the site
admin's; hate, discrimination and threats escalate regardless.**

⚠ **Site-admin removal ships in Slice 2, not here.** `User.IsAdmin` is a computed hardcoded Guid, so
it needs no permission flag, no migration and no backfill — which means every comment has a delete
button from the moment one can exist. That is what relaxes the "slices 2 and 3 must deploy together"
constraint in §10 into a preference. **The shield carries Remove and only Remove**: nobody edits
anybody else's words, including the owner's own.

**Personal notes are invisible to all of this** — never reported, never queued, never readable by any
moderator including the site admin, enforced where the rows are read.

### Permissions

A new flag on the existing `[Flags]` enum, which was built for exactly this:

```
ModerateComments = 1 << 4        // All: 15 → 31,  DefaultAdminPermissionsSeed: 13 → 29
```

- **Creators need nothing** — `PermissionsOf` returns `All` for the owner at read time.
- ⚠ `DefaultAdminPermissionsSeed` is **13**, not 15 — it deliberately excludes `PromoteAdmins`. So
  there are **two** populations to bump: `13 → 29` (the default kit grew) and `15 → 31` (explicit
  All). A hand-picked subset is left alone.
- ⚠ The backfill must hit **both** `CommunityMembership.Permissions` *and* `Community`'s stored
  `DefaultAdminPermissions`. Miss the second and every *future* admin in an existing club silently
  lacks the power, discovered months later.

### Sanctions

One table, two scopes:

`CommentRestriction(UserId, Scope: Site|Community, CommunityId?, RestrictedByUserId, Reason?, CreatedAt, LiftedAt?)`

- **Site scope** is the owner's content lock — blocks commenting everywhere.
- **Community scope** is the admin's mute — blocks that community only. This is deliberately
  *lighter* than the existing community ban, which ejects someone entirely; you stay in the club and
  lose the mic.
- Both are **prospective**. Existing comments stay unless separately deleted, matching the
  tool-maker ban pattern.
- A community **ban** already blocks commenting for free — no membership, no community comments.
- ⚠ Two Guid `*UserId` columns means `[PurgeKey(nameof(UserId))]` is required, exactly as on
  `CommunityMembership.GrantedByUserId`. Without it account deletion purges the wrong person.

### Reports route by audience

One Report action, a **closed reason vocabulary**, and the reason decides routing — the reporter
never has to work out whose problem it is, and the routing is **not advertised in the UI**.

| Reason | Goes to |
|---|---|
| Spam or advertising · Off topic · Wrong information | Community admins (or the site admin, if public) |
| **Hate or discrimination** · **Threats or harassment** | Community admins **and** the site admin |

⚠ **The report row stamps the rendering the reporter was reading.** Translation launders the thing
being detected, and the language-asymmetry case (benign Korean, hostile Spanish) is *only* a
moderation problem, because a moderator ever sees one language. The moderation view therefore shows
**the original and what the reporter saw**. Without it, an admin reading ko-KR cannot evaluate a
report filed against the es-ES rendering.

### Surfacing

A conditional panel — rendered only when that moderator has open reports — on the community admin
page and on `/Admin`. One row: difficulty bubble → song image → reported user → reporter → **Open**,
which launches the dialog with `InitialTab=Comments` and `FocusCommentId`. Moderation happens in the
surface the comment lives in; there is no second console to build or keep in sync.

The **shield glyph on a comment is the permission** — site admin sees it everywhere, a community
admin only inside their own club, nobody else renders it. Report lives in the `⋯` for everyone
signed in.

---

## 5. Deliberately out of scope

- **A notification system.** Owner, explicit. Accepted consequence: a reply three days later is a
  reply the asker never sees.
- **Comments on the chart page** (`/Charts/{mix}/{song}/{difficulty}`). Dialog only for v1. The page
  is static SSR and would need a second render path; public comments there would be strong
  crawlable content, so this is a likely follow-up rather than a rejection.
- **Cross-chart comment history for moderators.** Owner: avoid the surveillance UX problem for now.
- **A moderation *layer*** (automated flagging). Owner, and the corpus supports it — its three
  heated comments are all about charts, never identity, so an over-flagging layer catches all three
  and teaches everyone to ignore it. If ever added it must read the **original**, never the English
  pivot.
- **A public API surface.** Comments are not on `api/v1` or `api/v2`.

---
---

# Part 2 — Technical scope

> **Delete this entire part when the feature ships.** It describes how the thing gets built, not
> what it does. Durable facts (the vertical's package allowlist row, the retired token-spend
> invariant, the new Domain port) move to `CLAUDE.md` / `ARCHITECTURE.md` as they land, and do not
> survive here.
>
> §6–§9 are the whole feature. The Slice 2 build sheet that used to be §11 is gone — it was built;
> what it got wrong is recorded in §10 instead.

## 6. Verticals

### `ScoreTracker.ChartComments` — new

Owns comments, revisions, votes, restrictions, reports, consent records, and the renderings.
Standard vertical shape (`WeeklyChallenge` is the template): `Contracts/` and `Wiring/` public,
everything else internal.

Packages: `MediatR`, full `MassTransit` (the `AddChartCommentsConsumers` hook needs
`IRegistrationConfigurator`), `Microsoft.Extensions.DependencyInjection.Abstractions`,
`Microsoft.Extensions.Caching.Memory` (the tool-host allowlist cache). **Nothing new to the
solution** — one row added to CLAUDE.md's allowlist table.

### `ScoreTracker.Translations` — grows up

Today it owns no tables and is wired into nothing. It takes the **whole batch pipeline**: prompts,
glossary, batch-tracking tables, the spend ceiling, the admin page, and the
`ILanguageModelBatchClient` interaction. Its public surface becomes:

```
QueueTextForTranslationCommand(sourceKey, text)
    → publishes → TextTranslatedEvent(sourceKey, pivot, translations, provenance)
```

ChartComments publishes the command on post/edit and consumes the event to write renderings onto the
comment.

The rejected alternative was ChartComments building request payloads itself. That leaks
`LanguageModelRequest` shapes across the boundary and makes any prompt change a two-vertical edit.
The split above keeps prompt knowledge where the glossary lives, keeps *comment* data in the
comments vertical, and means community descriptions or tool blurbs can reuse the pipeline later
without a rewrite.

⚠ ARCHITECTURE.md currently describes Translations as *"owns no tables, references no `Data`, and is
wired into nothing."* That sentence dies in the same PR.

### Cross-vertical reads

Three references out of ChartComments, all through contracts, no cycles — nothing referenced here
references back.

| To | For | Via |
|---|---|---|
| `Communities` | Which clubs the reader may read and post to | `GetMyCommunitiesQuery` → `CommunityOverviewRecord`, which already carries `IsRegional`. **Add the community's `Guid` to that record** so a comment stores an id rather than a name — a rename would otherwise strand every thread |
| `Communities` | Whether the reader holds `ModerateComments` (**Slice 3 only**) | `GetMyCommunityRolesQuery` → `MyCommunityRoleRecord(CommunityName, Role, Permissions)`. It carries permissions but not `IsRegional`, so it answers the moderation question and *not* the audience one — two queries, not one |
| `CommunityTools` | Public tool URLs for the link allowlist | the existing public-tools query, memory-cached on a short TTL |
| `Translations` | Queue and consume | the command/event pair above |

Charts are **not** referenced. ChartComments stores a `ChartId` and knows nothing about charts; the
moderation queue row resolves song and difficulty in Web via Catalog, which keeps the boundary
clean for one join that only presentation needs.

## 7. Layers

| Layer | Change |
|---|---|
| **SharedKernel** | One line — `ModerateComments = 1 << 4`, `All` → 31. |
| **Domain** | One new port: `ILanguageModelBatchClient` (submit / status / results), beside `ILanguageModelClient`. `Complete()` is synchronous-per-request and cannot express a 24-hour batch. |
| **Application** | Nothing. It is shrinking by design and this does not reverse that. |
| **Data** | `AnthropicBatchClient : ILanguageModelBatchClient` in `Clients/`, plus migrations. Reflection DI binds it automatically. |
| **Web** | Dialog restructure + `ChartCommentsTab`, `CommentThread`, `CommentComposer`, `CommentTextView`, `CommentRulesCard`, `LinkInterstitialDialog`, `ReportCommentDialog`, `SimilarChartsCompactGrid`, `ChartScoreHistoryTab`, `ReportedCommentsPanel`. **No JS file** — plain text needs no `wrapSelection`. Plus `ChartCommentsConfiguration` + `IOptions<T>`, following `DevAuthConfiguration`. |
| **CompositionRoot** | `AddChartComments()`; `ChartCommentsModelContribution` into `VerticalModelContributions.All()`. |

### ChartComments internals

| Folder | Contents |
|---|---|
| `Domain/` | The `Comment` aggregate — audience invariant (including the `Private` rules in §2), edit→revision, soft delete, restriction gate. A `TournamentSession`-class rich model; the rules are dense enough to earn it. Plus `CommentText` (parser → token tree: lines, text, autolinked URLs) and `LinkTrust`, both pure. Vertical-internal repository ports. |
| `Application/` | `CommentSaga`, `CommentModerationSaga`, `CommentTranslationSaga` (consumes `TextTranslatedEvent`), read handlers. |
| `Infrastructure/` | EF entities + repositories on `Set<TEntity>()`, the `UserOwned` purge manifest. |
| `Contracts/` | Commands, queries, records, events. |

### The token tree crosses as a contract type

Each link node ships with its `IsTrusted` boolean **already resolved inside the vertical**. Web
renders nodes as Blazor elements and makes zero policy calls — it never sees the raw text, never
parses, never consults the allowlist, and `MarkupString` is structurally unreachable rather than
merely discouraged. The parser stays internal.

## 8. Two CLAUDE.md amendments

1. **`Anthropic` enters `ScoreTracker.Data`'s package allowlist**, plus the ChartComments allowlist
   row.
2. ⚠ **A documented safety property is retired on purpose.** Today nothing that ships can spend a
   token, precisely because `ILanguageModelClient` has no implementation in `Data` and no DI
   registration. This feature ends that. What replaces it: the submit-time ceiling check against
   rolling 30-day usage, the nightly count cap, and the admin page. That trade gets written down
   rather than discovered later.

## 9. Ratchets that bite if forgotten

| Miss | Consequence |
|---|---|
| `VerticalModelContributions.All()` | Scaffolded migrations silently drop every ChartComments table |
| `AddChartCommentsConsumers(...)` in `Program.cs` | MassTransit's scan skips internal types — CommunityTools once shipped with all 33 handlers unregistered and every suite green |
| Purge manifest **plus a real integration test** | A mocked purge test cannot catch over-deletion; only the decoy-account test can |
| `CommentEntity` in `UserOwned` | A blanket delete orphans every reply to a purged root. It belongs in `AccountPurgeCoverageTests.Exempt` with its reason; the tombstone step in ChartComments' purge repository is what actually clears it (§2) |
| **The audience filter, tested the same way** | A private note leaking is the worst thing this feature can do. The filter lives in the repository, and the decoy account holds a note on every scope that no other user's query may return |
| `AppHostForwardingTests` | `Configure<ChartCommentsConfiguration>(GetSection("ChartComments"))` is a newly bound section: it needs a `DeliberatelyNotForwarded` row (a mode flag, not a secret) plus `WithEnvironment` in the AppHost — which turns comments **on** for local dev and the E2E suite, so the E2E dialog tests gain a fourth tab |
| `[PurgeKey(nameof(UserId))]` on `CommentRestriction` | Two `*UserId` columns — purges the moderator instead of the restricted user |
| resx in all nine locales, alphabetical, no case-collisions | A case collision renders English in every non-English locale while English itself looks perfect |
| `SCHEDULED-JOBS.md` + `DATABASE-SCHEMA.md` rows | Same-PR requirement |

## 10. Build order

Four slices. Each ships something usable; the expensive and irreversible parts land last, on
foundations that already work.

| | Slice | Tabs | New tables | Spends money |
|---|---|---|---|---|
| **1** ✅ | **SHIPPED — [PR #227](https://github.com/DrMurloc/PumpItUpScoreTracker/pull/227)**, eleven commits, all five suites green. Sticky tab strip, `InitialTab`, Chart Stats absorbs the meta/skills/similar charts, Score History tab with the manual score edit | **3** | none | no |
| **2** ✅ | **BUILT** — comments + personal notes: vertical, aggregate, plain-text parser, link trust, threads, votes, composer, rules card, site-admin removal, `FocusCommentId` | **4** | yes | no |
| **3** | Community moderation — permission flag + backfill, restrictions, reports, queue panels | 4 | yes | no |
| **4** | Translation — batch client, the four-state pipeline, ceiling, admin page | 4 | yes | **yes** |

⚠ **Slice 1 ships three tabs.** A Comments tab with nothing behind it is not shippable, and
`FocusCommentId` has nothing to focus. The fourth tab arrives with the feature — which also means
the 390 px tab-strip overflow gets measured twice, at three and again at four.

### The launch toggle

Slice 2's comment surfaces are gated on `IsAdmin || ChartComments:Enabled`, configuration shipping
off, so comments are the owner's alone until production cost testing says otherwise. **The toggle
governs reading as well as writing**, which means flipping it publishes everything written during
testing at once — deliberate, and a cleanup pass before the flip is the owner's job.

**Personal notes ship ungated.** Nothing in a private note can go wrong in public, and it is the one
part of this that works on day one: comments need other people before they are worth reading, a note
to yourself works on an empty site.

⚠ **The old "slices 2 and 3 must deploy together" rule is retired.** It existed because a UGC surface
with no delete button is a bad week — but site-admin removal costs one `IsAdmin` check and rides
along in Slice 2, so every comment has a delete button from the moment it can exist. Shipping them
together is now a preference. Nothing is public either way until the toggle flips.

### What Slice 2 learned

The build sheet that used to sit here has served its purpose and is gone. What it got wrong, or
could not have known, is worth keeping:

- ⚠ **`VerticalAssemblies.All()` needed the new vertical**, and nothing in the build sheet said so —
  it is a second hand-maintained list beside `VerticalModelContributions.All()`, guarding MediatR
  rather than EF. `MediatRHandlerRegistrationTests` caught it, but only in the **integration** suite:
  every fast suite was green with every handler in the vertical unregistered. That is the exact
  failure CommunityTools shipped with.
- ⚠ **Comments cannot go in a `UserOwned` manifest, and neither can their votes or consents.** A
  blanket delete orphans replies; the vertical purges all four tables in one hand-written pass, and
  three `Exempt` rows carry the reason. Revisions are the easy one to miss — no user key, and they
  hold the text the purge exists to remove.
- ⚠ **A resx insert can land inside the ResX schema comment.** It contains example `<data>` elements,
  so an alphabetical scan matches one and the keys compile to nothing while English looks perfect.
  `NoResxDataElementsHideInsideTheSchemaComment` catches it. Skip comment ranges, and back up over an
  explanatory comment so it stays attached to the entry it explains.
- **`CommunityOverviewRecord` gained a required `CommunityId`.** Required rather than optional turned
  a silent `Guid.Empty` into nine compile errors — worth the churn for a foreign key.
- **Domain gained one type after all**: `CommentNotAllowedException`, which Web catches by type to
  show the reason. The build sheet said "Domain: nothing", which was wrong by one file.
- **The three "still open" questions were answered before the build**: purge tombstones roots with
  replies and hard-deletes the rest; comments are cross-mix (one `ChartId` spans mixes, and the steps
  do not change); leaving a community keeps your comments in it and takes away your ability to read
  them.

**Still owed, and only measurable by hand: the 390 px pass.** Open the dialog at 390 with all four
tabs and confirm `scrollWidth > clientWidth` on the tab strip and nowhere else. Two candidates
besides the strip: the scope rail, which wraps and so must never scroll, and — the likelier — a bare
URL in a comment body, which is what `overflow-wrap: anywhere` on `.cmt-body` is for.

### What Slice 1 left open

Everything needed is in this folder: Part 1 for what to build, Part 2 for how, [mock.html](mock.html)
for what it looks like — including the rules-card copy (§03), which is written, not a placeholder.

Two things Slice 1 left open:

- **The 390 px layout is unmeasured.** Three tab labels and the three-column similar-charts grid
  both live on that screen. Open the dialog at 390 and confirm `scrollWidth > clientWidth` on the
  tab strip and nowhere else. The fourth tab arrives with Comments, so measure again then — and
  with two new candidates: the scope rail, which wraps and so must never scroll, and (the likelier)
  a bare URL in a comment body, which is what `overflow-wrap: anywhere` on the comment body is for.
- **`ChartClickContext` still carries `SuggestionCategory` and nothing reads it.** Left deliberately:
  it is part of the widget render contract `WidgetRenderContractTests` ratchets, so pruning it is a
  contract change across every widget that constructs one, not a cleanup to fold into other work.
