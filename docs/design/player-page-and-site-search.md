# The player page and site search — design

> **Status: BUILT** (workshopped and built 2026-08-16, one PR). Mock, owner-approved through four
> rounds: <https://claude.ai/code/artifact/027a2f08-73ef-4970-94f8-fe73daa42559>.
>
> Companion: [peers-abstraction.md](peers-abstraction.md) — the *drafting* doc for the pool
> abstraction this work deliberately leaves room for and does not build.

Two things, one PR:

1. **One player page.** `/Community/Player` and the inline head-to-head on `/Rivals` were halves of
   the same page grown apart — the community page had the profile (hero, ratings, official
   standing, folder completion) with a plain compare table; the rivals compare had the better
   table (art, plates, click-to-chart, "All folders", legacy-aware, ghost-capable) with no
   profile. `/Player/{id}` is the one page both link to. It is the root of a family that already
   existed: `/Player/{id}/Sessions` and `/Player/{id}/PhoenixRecap`.
2. **Players in the search bar.** The app-bar search grows two sections under the chart matches:
   players on this site, and players on the official boards. Selecting one lands on the player page
   or the official player page respectively.

---

## 1. Locked decisions

Owner calls, 2026-08-16, in his own words where it matters.

### The page

| # | Decision |
|---|---|
| D1 | **One player page, retiring both.** `/Community/Player` is deleted (its one link, the community leaderboard row, repoints); the inline compare on `/Rivals` is retired for site rivals — Compare navigates to `/Player/{id}`. |
| D2 | **The ghost head-to-head stays inline on `/Rivals`, and only for board-only rivals.** A ghost has no `/Player/{id}`; the official Players page is **not** changed — "leave them EXACTLY as they are" — so the mirror-only compare keeps its home on the rivals page. |
| D3 | **Access = self ∪ public ∪ shares a user-created community with you ∪ you hold a rival edge onto them.** The union of what the two retiring pages allowed; nobody gains or loses visibility. World and Region do not count as shared communities. |
| D4 | **`/Sessions` and `/PhoenixRecap` stay public-or-self.** The page links them only when they would open. |
| D5 | **The compare counts comparable pairs** (the rivals semantics: You ahead / They ahead / Shared), with a **Show unshared** switch — off by default — that reveals the rows only one of you has played and two more tiles (Only you / Only them). The community page's "you win because they never played it" counts are gone. |
| D6 | **The `{N}y old` stale marker is dropped** — it was the community page's; rivals never had it. So is the Winner column (colour carries it) and the community breadcrumbs. |
| D7 | **The hero**: PUMBILITY with the Phoenix 2 level gem beside it, then the identity, **on one row**, stacking only at 1:1 aspect or narrower; the four rating tiles a full-width row beneath. |
| D8 | **The overall competitive level never shows.** Singles and Doubles competitive levels, stacked (the account widget's pool-row treatment). |
| D9 | **The "N top-board charts →" link lives on the Official Boards card**, not the hero. |
| D10 | **Tags under the name are `Rival` and the shared community's own name.** The word "clubmate" is not used anywhere in copy. |
| D11 | **No nav entry.** Your own page is reachable through leaderboard rows and by searching yourself. |

### The search

| # | Decision |
|---|---|
| D12 | **Three sections, fixed order: Charts → Players → Board players**, capped 8 / 4 / 4. Charts stay client-side, as today. |
| D13 | **No minimum length.** Players named "!", a lone emoji, or "D" must be reachable from one character; what makes that work is ordering — exact → starts-with → contains, then alphabetical — with the ordering and the cap pushed into SQL. |
| D14 | **A player matches on site name or piugame game tag.** |
| D15 | **The Players section searches exactly the pool the page's access check uses** (D3), public-only when signed out. |
| D16 | **Rows glow, no chips.** A player in one of your communities is a green row, a rival red, both split — the `.is-community` / `.is-rival` / `.is-both` states every board already uses. |
| D17 | **A linked board player appears in both sections** — Players → their page, Board players → their official page. No dedupe. |
| D18 | **The rival picker reads the same visibility abstraction**, which also fixes it: it claimed "public ∪ your community members" but Identity's search dropped private users before the picker could OR them back in, so a private member of your community was unreachable there. |
| D19 | **The official Players page is untouched, now and later.** Search links to it; nothing on it changes; it is not folded into `/Player/{id}`. |

---

## 2. The model

### 2.1 Visibility is a port, not a Rivals contract

Two questions live in this feature and they are not the same question (see
[peers-abstraction.md §1](peers-abstraction.md)):

- **who may I look at** — consent grants: self, public, shared user-created community, rival edge;
- **who am I measured against** — peer pools, which this PR does not touch.

The first is `IPlayerVisibilityReader`, a published Domain port:

```
IPlayerVisibilityReader
  Task<PlayerVisibility> Describe(Guid? viewerId, Guid targetId, ct)
  Task<IReadOnlySet<Guid>> VisibleUserIds(Guid? viewerId, ct)   // self ∪ community members ∪ rival targets

PlayerVisibility(bool CanView, bool IsYou, bool IsPublic, bool IsYourRival,
                 IReadOnlyList<Name> SharedCommunities)
```

`Public` is a predicate, not a member of the set — the set is the *extra* people a private viewer
may see. **Rivals implements the port for now**: it is the one vertical that can already see
Identity, Communities and its own edges, and it computed two of the four bases before this work.
It is the temporary host, not the owner; when a Peers vertical exists it takes this implementation
and nothing that consumes the port moves.

Consumers: `PlayerProgress`'s profile handler (gated inside — a private player's folder completion
is never one un-gated send away), `Identity`'s player search (its population), the page (its gate
and its hero tags, off the same record), and the rival picker.

### 2.2 The reads

| Read | Vertical | Shape |
|---|---|---|
| Profile | PlayerProgress | `GetPlayerProfileQuery(userId, mix)` → `PlayerProfileRecord?` — name, avatar, country, PUMBILITY, ratings, **Singles/Doubles** competitive levels, highest clear, folder completion, and the `PlayerVisibility` it was gated on. Null when you may not look. Moved from `CommunityPlayerSaga`, legacy branch included, minus the overall competitive level. |
| Head-to-head | Rivals | `GetPlayerHeadToHeadQuery(mix, opponentUserId, chartType?, level?)` → the existing `RivalHeadToHeadRecord`, gated by the port; the edge-keyed `GetRivalHeadToHeadQuery` stays for ghosts. Rows now include one-sided entries and the record carries `OnlyYou` / `OnlyThem`, so the switch is client-side. Folder mode = the folder's chart list; All folders = the union of both score sets. `RivalSubject.EdgeId` is nullable — a subject you may compare with need not be your rival. |
| Player search | Identity | `SearchPlayersQuery(term, take)` → hits with the visibility bases for the row glow; the whole predicate is SQL (`Name` or `GameTag` LIKE, `IsPublic OR Id IN @visible` via `OPENJSON`, exact/prefix/contains order, `TOP`). `SearchForUsersQuery` is untouched — the session builder asks it for a thousand rows; different job. |
| Board search | OfficialMirror | `SearchOfficialBoardTagsQuery` returns `(Username, AvatarUrl?, IsLinked)` instead of bare strings, same ordering rule. |
| Community members | Communities | `GetMyCommunityMembersQuery()` — one read for the member ids of your user-created communities, replacing the per-community loop the picker did per keystroke. |

The comparison rule (comparable / legacy tie-break / margin / deficit-first order) is not
duplicated: the head-to-head engine stays in Rivals with a second entry point, and is earmarked to
move with the pool abstraction. A second implementation of that rule is exactly the drift this
codebase keeps paying for.

### 2.3 What was given up

Recorded so a future complaint maps to a known item ([retire, don't port]):

- the community compare's whole-folder counts, where a chart they never played was your win;
- the `{N}y old` marker on a compare cell;
- the Winner column;
- "Back to community" / breadcrumbs on the profile — the page has no community context; the
  browser's back button is the way home;
- the community page's XX compare, which was empty anyway (the handler read the Phoenix store
  unconditionally); the rivals engine is legacy-aware, so the page's compare works on XX.

---

## 3. The surfaces

### 3.1 `/Player/{id}`

Interactive page. On load: `GetPlayerProfileQuery` — null redirects home, the Sessions pattern.
Sections: hero (D7–D10; the gem only on Phoenix 2, `PumbilityLevelBadge` self-hides on a 404) →
Official Boards card, only when the account is import-linked, with the top-board chip (D9) →
Folder Completion (the community page's two spectra) → head-to-head, signed in and not you (D5) →
Sessions / Recap links, public or you (D4). Legacy mixes hide the Phoenix-lineage numbers exactly
as the community page did; the folder graphs read the legacy store.

### 3.2 `HeadToHead` (`Components/Players/`)

`RivalComparison` moved and grown: the switch, dashed one-sided rows, the two extra tiles, a pager.
Two hosts — the page, and `/Rivals` for board-only rivals (D2).

### 3.3 `/Rivals`

Compare on a site rival navigates to `/Player/{id}`; on a ghost it opens inline, as before.

### 3.4 The search

`AppBarSearch` hosts `SiteSearch` at both mounts (desktop app bar, phone sheet). One
`MudAutocomplete<SearchHit>`; the section labels are non-selectable rows; the chart section runs on
`ChartSearchIndex` — the shorthand parse and name index extracted from `ChartSelector` so both share
one implementation; the two server sections debounce and abandon a superseded search the way the
rival pickers do (a cancelled `SqlCommand` surfaces as a provider error, so the filter is on the
token, never the exception type). Enter: chart → its canonical page; player → `/Player/{id}`; board
player → `/OfficialLeaderboards/Players?player=`. The sheet label becomes "Find a chart or player".

---

## 4. Vocabulary

- **Never "clubmate."** Say the community's name.
- **Never the overall competitive level.** Singles and Doubles.
- **Row glow, not chips**, for "in a community with you" / "your rival" — the highlight vocabulary
  is ubiquitous already.
- **PUMBILITY renders `N0`** here — this is not a PUMBILITY-section page.

---

## 5. Commit order (as built)

Docs first, localization last, one PR. ⛓ = hard constraint.

1. `docs(players)` this doc · 2. `docs(peers)` the drafting doc
3. ⛓ `feat(domain)` the port + the `IUserRepository` search method with its EF implementation
4. `feat(communities)` batched member read · 5. `feat(rivals)` the visibility reader + the picker
6. `feat(progress)` the profile read · 7. `feat(rivals)` user-keyed head-to-head
8. `feat(identity)` player search · 9. `feat(mirror)` board search returns a record
10. ⛓ `refactor(web)` `ChartSearchIndex` extraction · 11. `feat(web)` the head-to-head component
12. `feat(web)` the player page · 13. ⛓ `refactor(communities)` retire the two reads
14. `feat(web)` the site search · 15. `test` real-DB · 16. `docs` close-outs
17. ⛓ `feat(l10n)` all nine locales, last

---

## 6. Testing

| Suite | What lands there |
|---|---|
| `Tests/ApplicationTests` | the visibility reader (all four bases; anonymous = public only), the gate on the profile and head-to-head handlers, one-sided rows and legacy in the user-keyed head-to-head, the Identity search handler, the batched member read |
| `Tests.Integration` | `EFUserRepository` search — exact/prefix/contains order, a game-tag hit, a single-emoji name, a private player reachable only through the visible set, the cap; the board-tag ordering |
| `Tests.Components` | the page's gate outcomes, the hero's mix gating, the head-to-head switch, `SiteSearch` sections / caps / targets at both mounts, `/Rivals` compare routing |
| `Tests/ArchitectureTests` | the ratchets pick up the rest — message taxonomy, vertical surface, resx order, colour tokens |
| `Tests.E2E` | nothing new |
