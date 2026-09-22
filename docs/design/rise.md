# Pump It Up RISE — two keyboard mixes

Status: **design, no code.** Researched 2026-09-14 → 2026-09-21 from the owner's install, his screenshots, two
community sheets, two wikis and the Steam patch notes; the owner took the high-level plan to two Rise players
(Sneezle, Dave) on 2026-09-21 and their answers are folded in. Every decision below is the owner's where marked;
the rest are *decided unless he objects*. The research bundle (every source file, the exported game art, the
result-screen table) is in the owner's `Downloads\rise-research-2026-09-14\`.

Three phases (owner, 2026-09-21): **1.** add RISE as two mixes with their catalog, scoring rules, art and
theme, recording through spreadsheet upload and manual entry; **2.** a desktop capture app that reads the game's
result screens, in a screen-grab mode and a Steam-screenshot (F12) mode; **3.** leaderboards and PUMBILITY for
RISE, with tuning. Only phase 1 is scoped here; §8 sketches the other two so phase 1 leaves the right seams.

---

## 1. What RISE is

PUMP IT UP RISE (Steam app 2756930, Unity/IL2CPP, Andamiro) is the official PC port: Early Access 2025-07-15,
full release 2026-03-26 (v1.0.0), updates roughly monthly (1.5.0 in September 2026). It is played with hands —
keyboard, gamepad, Steam Deck. **One game holds two separate ecosystems, and records never cross between them**
(owner-verified in-game: a score set in one does not appear in the other).

| | **RISE mode** (Warm Up · Division · Challenge · WorldMax) | **Arcade Station** (added 1.4.0, July 2026) |
|---|---|---|
| Chart types | 5K SINGLE, 6K H.DOUBLE (the arcade Half Double layout) | 5K SINGLE, 10K DOUBLE |
| Charts | ports "pattern optimized for keyboard"; half-doubles are re-cut, re-rated doubles | the arcade charts as-is, at **Phoenix 2** levels and note counts |
| Songs | 421 (base 283 + REMIX 37 + RISE Vol.2 20 + PHOENIX 2023 20 + PHOENIX 2024 26 + contest 5 + updates) | 352 = the RISE songs that existed in Phoenix 1, minus the RISE and CONTEST channels; **only songs you own are listed** |
| Score | Phoenix formula, verified to the point on 20 screens (§5.1) | same formula |
| Grades | **nine**: SSS SS S AA A B C D F — no plus tiers, no AAA (§5.2) | the full Phoenix ladder with plus tiers, on the **Phoenix 2** floors |
| Awards | three **marks**: PERFECT GAME · FULL COMBO · NO MISS (§5.3) | all eight Phoenix plates |
| Holds | the head of every hold is a judged tap; pre-holding scores a MISS | arcade behavior: pre-holding starts a PERFECT chain |
| Lifebar | its own HP model; Warm Up is "break off" (HP 0 does not end the song) and a run that emptied the gauge shows a grey grade | "break on": HP 0 ends the song, like the arcade; no pause |
| Scores stored by the game | per chart: BEST SCORE, ACCURACY, MAX COMBO; BREAK TIME lists the last 50 plays with grade, score and date | same |

**Nothing exports.** Steam Cloud saves are AES-encrypted blobs, the game's own song database ships encrypted the
same way, the game server (`riselive.piugame.com`) has no web surface, and Steam publishes no leaderboards for the
app. Manual entry and spreadsheet upload are the only score paths until the capture app exists. "Plate" in RISE's
own vocabulary is a cosmetic profile banner, not a score award.

RISE songs and half-doubles also reach the Phoenix 2 **arcade** as a "RISE MIX" channel for AM.PASS-linked
players. That is the arcade's side of the link, not a score path for this site, and **Phoenix 2 will not carry
those charts** (D8).

---

## 2. Locked decisions

Owner decisions are marked **(owner, date)**; the rest are mine, decided unless he objects.

- **D1 (owner, 2026-09-21). Two mixes: `Rise` and `RiseArcade`.** Separate ecosystems, separate records,
  separate vocabularies. Display names "Rise" and "Rise Arcade"; `scores.Mix.Name` keeps `MaxLength(10)`, so the
  enum names are the DB names (`RiseArcade` is exactly ten characters, the `Phoenix2` precedent). Sort order after
  Phoenix 2. Both are top-level in the mix picker, since they are the only mixes still receiving updates (decided
  unless objected; §9 Q4).
- **D2 (owner, 2026-09-15, confirmed by the phase-1 scope). Phoenix-scored storage from day one.** Both mixes
  answer false to the legacy-scoring check: records live in `PhoenixRecord`, the journal writes its Phoenix side,
  `BestAttemptPolicy` applies. Storing RISE as a legacy mix and flipping later would strand every phase-1 row on the
  wrong side of the journal, the scar recorded in [legacy-mixes.md](legacy-mixes.md).
- **D3 (owner, 2026-09-15). N kinds of mix, not a third boolean.** The mix profile (§3) becomes the source of
  truth for everything that varies per mix; the existing helpers become reads off it.
- **D4 (owner, 2026-09-21). Rise Arcade's catalog is Phoenix 2's.** Membership from Dave's hand-made list;
  charts, levels and note counts from our own Phoenix 2 rows (§4.2). The players call the Arcade Station "a
  pseudo Phoenix 1 port", which is true of the *song list* only: levels, note counts, grade floors and plates all
  measure as Phoenix 2 (§5.4).
- **D5 (owner, 2026-09-21). The three marks ship in phase 1 as the RISE plates**, stored as the Phoenix plate
  with the same rule (§5.3). No new field, no new enum.
- **D6 (owner, 2026-09-21). The "passed in another mix" border stays, and never crosses platforms.** A keyboard
  pass never lights an arcade tier list and vice versa. The profile carries a platform (pad | keyboard) and the
  cross-mix pass union joins only the viewer's platform. Load-bearing: Rise Arcade sits on the same chart ids as
  Phoenix 2, so without the gate a keyboard clear would draw the border on the Phoenix 2 tier list the day RISE
  ships.
- **D7 (owner, 2026-09-21). Rise singles map onto their arcade charts; half-doubles do not map onto doubles.**
  A ported single is a membership row on the existing arcade chart at RISE's own level; every half-double is a new
  chart row (§4.3). A link between a half-double and the full double it was cut from may come later, as a link, not
  an identity.
- **D8 (owner, 2026-09-21). Phoenix 2 gets no half-doubles.** On the arcade they behave like UCS: no personal-best
  or recent-play rows on piugame, no PUMBILITY. If they are ever added it is an entirely separate UX.
- **D9 (owner, 2026-09-21). RISE's lifebar is not modeled.** Both mixes differ from the arcade bar (the owner's
  observation; patch 0.5.1 rebalanced starting HP, max HP and per-judgment recovery). The profile says
  `LifebarModel = None`; a broken run is a flag the player sets, and the stage-break cause solver never runs on
  these mixes.
- **D10. Note counts are per mix and learned, never derived.** RISE-mode judgment totals do not equal the arcade
  note counts (§4.5); `ChartMix.NoteCount` already exists per mix and starts null for Rise. The Arcade Station's
  totals equal our Phoenix 2 counts exactly, so Rise Arcade rows inherit them.
- **D11. The RISE grade ladder is the published one** (§5.2), with the unmeasured low floors as a placeholder to be
  corrected from the owner's runs. Safe because a grade is derived from the score at read time and never stored.
- **D12. Art.** RISE's own grade and mark art, exported from the game files, serves the Rise mix; Rise Arcade
  reuses the site's Phoenix art, which is what the Arcade Station itself draws. Each mix gets its own palette
  (owner, 2026-09-21).

---

## 3. The mix profile

Today one boolean, `UsesLegacyScoring()`, answers six questions, and five of them are the wrong question for RISE:
it is Phoenix-scored, yet it has no official site, no lifebar model, a different grade ladder and a different award
vocabulary. `PiuGameConfiguration.BaseUrlFor` throws for any mix but the two Phoenix mixes; the five
`MixCapabilities` flags, the difficulty-bubble art routing, four "every non-legacy mix" fan-outs and the
cross-mix pass union all read that boolean.

`MixProfile` (SharedKernel, one record per `MixEnum` value, `MixProfiles.For(mix)`; a DomainTest asserts every enum
value has one):

| Field | Phoenix / Phoenix 2 | Rise | Rise Arcade | Legacy mixes |
|---|---|---|---|---|
| `ScoringModel` | Phoenix | Phoenix | Phoenix | Legacy |
| `GradeLadder` | Phoenix1 / Phoenix2 | **Rise** (nine grades) | Phoenix2 | — |
| `Awards` | Phoenix plates (8) | **RiseMarks** (3, §5.3) | Phoenix plates (8) | None |
| `OfficialSite` | Phoenix1 / Phoenix2 | None | None | None |
| `Platform` | Pad | **Keyboard** | **Keyboard** | Pad |
| `LifebarModel` | Phoenix | None | None | None |
| `ChartTypes` | S, D, CoOp, SP, DP | S, **HalfDouble** | S, D | per mix |
| `Art` | own palette, bubbles, letters | own palette, **Rise letters + marks**, chip bubbles | own palette, Phoenix letters/plates/bubbles | Phoenix letters, XX bubbles, chips |

The existing helpers stay and delegate: `UsesLegacyScoring()` reads `ScoringModel`, the `MixCapabilities` flags
read the profile, `LetterGradeFor` / `GetMinimumScoreFor` / `GetMaximumScoreFor` pick the floors table off
`GradeLadder`. The ~80 call sites that ask "is this Phoenix-scored?" keep compiling and stay right. The dozen that
were asking the wrong question move to the right field:

| Call site | Asks today | Should ask |
|---|---|---|
| `PiuGameConfiguration.BaseUrlFor` | mix switch, throws | `OfficialSite` |
| `MixCapabilities.HasOfficialBoards` / `HasWeeklyBoard` / `HasMarchOfMurlocs` / `HasPumbility` / `HasPhoenixCalculators` | `!UsesLegacyScoring()` | explicit per-profile answers (phase 1: all false for both RISE mixes; phase 3 revisits) |
| `ShareCardImages.DifficultyBubble`, `LetterGrade`, `Plate`; `DifficultyBubble` | legacy check → flat vs per-mix folder | `Art` |
| `GetCrossMixPassesHandler` | every non-legacy mix | every Phoenix-scored mix **on the viewer's platform** |
| `BackfillStageBreakCausesConsumer` | every non-legacy mix | every mix with a lifebar model |
| `BackfillMaxCombosConsumer`, `BrokenRecordCleanupSaga` | every non-legacy mix | unchanged in effect (they want Phoenix-scored) — read `ScoringModel` |
| `RecordScoreForm` plate picker, `PhoenixScoreFileExtractor` plate shorthand, `PhoenixPlateHelperMethods.GetName` | the eight plates, always | the profile's `Awards` (values + display names) |
| `ApiMixParser` (v1) | Phoenix, Phoenix2 only | every Phoenix-scored mix (§6.3) |

Adding a mix after this is a profile row, an enum value, a `MixIds` Guid and a `scores.Mix` row.

---

## 4. Catalog

### 4.1 Rise

- **Source of truth (owner):** the community sheet
  [The_N_1 PIU Rise Song List](https://docs.google.com/spreadsheets/d/1ke_UDSBKu5a0W3-U9uRfMHj6yb4Nn5dQe3CQh8KfTLI)
  — 421 songs as of 2026-09-21, Single and Half-Double levels, category/channel, the RISE version each song arrived
  in (0.2.1 … 1.5.0), DLC, plate availability. It is live-maintained: every song named in every Steam patch note
  through 1.5.0 is in it.
- **New-song oracle:** the Steam news API
  (`api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=2756930`) — every patch note carries a
  Title / Artist / BPM / Channel table. The official YouTube channel posts no RISE chart videos, so the
  check-new-charts skill cannot be repurposed; a RISE variant walks the patch notes instead.
- **Launch metadata:** the Pump Pro+ launch PDF (BPM for launch songs), the Steam DLC store pages (track lists).
- **Songs new to the tracker:** ~45 (10 RISE Vol.1 originals not already in the arcade, all 20 Vol.2, five
  Variety crossovers, the contest five, a few spellings). They need Song rows with artist and BPM (both in hand)
  and jackets (§9 Q2).

### 4.2 Rise Arcade

- **Membership:** Dave's hand-made list
  ([sheet](https://docs.google.com/spreadsheets/d/16Ct-Gjg7DzQuUkZrCDQ_prDNhV-LlKoFRzA2Wku9dvU), copy in the
  bundle): 352 songs = 240 era-channel songs + 29 Variety + 20 PHOENIX 2023 + 26 PHOENIX 2024 + 37 REMIX. Both of
  the owner's in-game counters reconcile to it (269 before he owned the DLC, 352 after).
- **Rule that reproduces it:** the RISE songs that exist in Phoenix 1, minus the RISE and CONTEST channels
  (exactly 29 of the sheet's 45 Variety songs exist in Phoenix 1; the four that debuted in Phoenix 2 are absent).
- **Charts, levels, note counts: ours, Phoenix 2's.** Song by song on the full Single+Double level sets, 349 of
  352 match our Phoenix 2 rows exactly, 153 of them match Phoenix 2 *only*, none match Phoenix 1 only; the three
  misses are one-digit typos in the hand-made list. Two result screens confirm note counts (4NT S22 1,100 = 1,100;
  Curiosity Overdrive S20 1,101 = 1,101). So Rise Arcade is **membership rows on the existing Phoenix 2 charts**,
  and nothing has to be typed.

### 4.3 Identity rules

| RISE chart | Treatment | Count today |
|---|---|---|
| Single at the same level as an arcade single of that song | membership row on that chart | 1,422 |
| Single one level off an unused arcade single | a re-rate: membership row on that chart at RISE's level | 120 |
| Single with no arcade single within one level | new chart row, `OriginalMix = Rise` | 28 |
| Singles on songs the tracker lacks | new song + new chart rows | 248 charts on 57 songs |
| Every half-double | new chart row, `OriginalMix = Rise`, `Type = HalfDouble` | ~1,000 |
| Arcade singles RISE does not carry | nothing | 43 |

The 120 + 28 need the owner's eye before the script runs (the per-song comparison CSV is in the bundle). Sneezle's
"the note count should match" test cannot arbitrate them: RISE changed hold sections and counts hold ticks
differently (§4.5), so totals differ even on identical step patterns.

### 4.4 Versions

The sheet's Version column is a RISE patch (0.2.1 … 1.5.0) and the Steam news posts give each patch its date, so
the Rise mix gets `MixVersion` rows and every membership row an `AddedInVersionId`, the vocabulary from
[chart-versions.md](chart-versions.md). Rise Arcade's rows carry the later of the song's RISE version and 1.4.0
(the station's launch); DLC songs appear there when owned.

### 4.5 Note counts

RISE-mode judgment totals against our arcade counts: 4NT S22 **1,078 vs 1,100**; Darkside Of The Mind S23 1,333 =
1,333 (twice); Aragami S22 **1,356 vs 1,349**. The Japanese wiki says RISE changed charts in long-hold sections
(added taps, other panels) and the Korean wiki that hold ticks are judged differently — so RISE totals are neither
the arcade's nor the arcade's plus one per hold. They are learned per chart from result screens (every capture
carries the five counts), stored in the Rise membership row's `NoteCount`, and stay null until seen. Everything that
reads a note count (the max-combo solver, the stage-break solver, the calculators) treats null as unknown, which it
already does.

---

## 5. Scoring, grades, marks, breaks

### 5.1 Score and accuracy (both mixes)

```
accuracy = (Perfect + 0.6·Great + 0.2·Good + 0.1·Bad) ÷ notes
score    = floor(995,000 · accuracy + 5,000 · maxCombo ÷ notes)
```

Reproduced to the point on every single-song result screen we have (guidebook screens plus 18 of the owner's).
The ACCURACY field is that same term shown as a percentage **truncated** to two decimals (93.8073 → 93.80,
96.4396 → 96.43). Judgments are P/G/Gd/B/M; GOOD holds the combo without advancing it, BAD and MISS break it. A
Challenge result is an aggregate (score = the sum over its 2–4 songs, accuracy = the per-song mean) and is never a
chart score.

### 5.2 The Rise ladder

| Grade | Floor | Source |
|---|---|---|
| SSS | 990,000 | JP wiki; owner's screens bracket the cut in (985,823 … 991,124] |
| SS | 970,000 | JP wiki; bracket (968,683 … 974,233] |
| S | 950,000 | JP wiki; bracket (949,834 … 953,852] |
| AA | 900,000 | JP wiki; bracket (848,246 … 915,325] |
| A | 750,000 **?** | JP wiki's own question mark; nothing below 826,833 measured |
| B, C, D, F | unknown | placeholder: Phoenix 1's 650k / 550k / 450k until the owner's low runs land |

No plus tiers, no AAA: it is Phoenix 1's SSS / S / AAA / AA floors with each letter shifted down one rung. The
ladder is a floors table like the two Phoenix ones; the lookup walks it unchanged. **Rise Arcade uses the Phoenix 2
table** — its 919,853 read A+, which only the Phoenix 2 floors produce.

### 5.3 Marks are three of the Phoenix plates

| RISE shows | Rule (Korean wiki, two phrasings; guidebook combo rule) | Stored as |
|---|---|---|
| PERFECT GAME | every note Perfect | Perfect Game |
| FULL COMBO | every note Perfect or Great — "zero Goods or lower" | **Ultimate Game** |
| NO MISS | zero misses, at least one Bad or Good | Superb Game |

A run with Goods but no Bads or Misses is a Phoenix Extreme Game and displays as NO MISS in RISE. Derived from
judgments on a capture, the exact Phoenix plate is stored; on manual entry the player picks one of the three and the
site stores the weakest plate that guarantees it. Display names come from the profile. The site's plate tolerance
table needs no change.

### 5.4 Rise Arcade

Phoenix 2 in every measured respect: the full plus-tier ladder on Phoenix 2 floors (919,853 = A+), all eight plates
by the site's own tolerances (26 misses = Rough Game, 14 = Fair Game), Phoenix 2 levels and note counts, arcade hold
behavior, break on.

### 5.5 Broken runs

RISE mode: Warm Up plays through HP 0 ("break off"); a run in which the gauge emptied at least once shows its grade
in grey and earns no EXP (Japanese wiki; a Steam thread). The Arcade Station is break on and the song ends. The site
stores broken as the player-supplied flag it already has; nothing is inferred (D9). Open (§9): the owner suspects
the grey needs both an empty gauge and a sub-S grade — two runs settle it, and nothing in phase 1 depends on the
answer.

---

## 6. Recording

### 6.1 Spreadsheet upload

`PhoenixScoreFileExtractor.GetScores(file, mix)` is already mix-parameterized (Song, Difficulty, Score, Plate,
IsBroken) and `DifficultyLevel.ParseShortHand` already reads `HDB16`. What is missing is a page: `/UploadPhoenixScores`
is the piugame-credential importer and refuses a legacy mix; a Phoenix-scored mix with no official site needs the
spreadsheet flow alone. One page, driven by the profile: on an official-site mix it is today's importer, on a
site-less Phoenix mix it is the upload alone. Plate shorthand accepts the RISE marks (`PG`, `FC`, `NM`) beside the
Phoenix codes and stores per §5.3.

### 6.2 Manual entry

`RecordScoreForm` already adapts by scoring model and reads its prefill from the store the mix uses. Its plate list
becomes the profile's award list with the profile's names.

### 6.3 API

`POST api/phoenixScores` (song, type, level, score, plate, broken, mix; `KeepBestStats`) is what the phase-3 capture
app will call, but v1's `ApiMixParser` admits only Phoenix and Phoenix 2 by design. Phase 1 widens it to every
Phoenix-scored mix (a golden-file change in `Tests.Api`, additive); v2's `scoringModel` already derives from the
profile and needs nothing.

---

## 7. UI

- **Theme:** two new `MixPalette`s and two CSS classes, both added to `ThemedMixes` so neither silently falls back
  to Phoenix. Rise's palette comes from the game's own UI (hot pink and cyan on navy, yellow accents); Rise Arcade's
  from the Arcade Station's screens.
- **Letters and marks:** RISE's native sprites, exported from the install, clean and named — nine grades in large
  and small sizes, the nine grey broken variants, the three marks in badge and word-art form (bundle,
  `2026-09-21/art/`). `ShareCardImages.LetterGrade` / `Plate` gain the per-mix folder that `DifficultyBubble` already
  has (`letters/{mix}/…`, `plates/{mix}/…`, flat fallback), and the owner uploads the set to the CDN. Rise Arcade
  points at the Phoenix set.
- **Difficulty:** half-doubles already render as the CSS chip on any mix. Rise singles: chips styled on the game's
  own red (5K) and blue (6K) level badges, so the whole row is one vocabulary and there is no asset dependency
  (§9 Q3). Rise Arcade reuses the Phoenix 2 bubbles, which is what the station draws.
- **Picker:** both mixes top-level (D1). Phase-2 pages answer through `MixUnavailableNotice` ("not built yet"
  for PUMBILITY, weekly boards, March of Murlocs; "this mix never had it" for official boards and titles).
- **Localization:** mix names stay untranslated proper nouns; the mark names and new page copy land in all nine
  locales in the same pass.

---

## 8. Phases

**Phase 1 — core game system + songs/charts.** The profile and the call-site migration (§3); the two enum values,
`MixIds`, the two `scores.Mix` rows, palettes and CSS; the Rise ladder and the award vocabulary; per-mix letter and
plate art paths; the catalog scripts (Rise: songs, charts, membership rows, versions; Rise Arcade: membership rows
on Phoenix 2 charts, versions) as idempotent SQL in Downloads plus the generator in `tools/`, the legacy-mix
precedent; the upload page and manual entry; the v1 mix parser; docs (this, DOMAIN.md for the RISE terms,
API.md for the mix values, UX-GUIDELINES.md for the palettes); tests (profile completeness, ladder, mark mapping,
platform gate on the cross-mix union).

**Phase 2 — the capture app**: screen-grab and F12 modes, template-matched
digits on the fixed result layout, the score and accuracy checksums, chart resolution against the Rise catalog,
`POST api/phoenixScores`. Skips Challenge aggregates (division badge in the header). Learns note counts (D10).

**Phase 3 — boards and PUMBILITY** with their own tuning; revisit the capability answers.

---

## 9. Open questions (phase 1)

1. **Low grade floors.** Ship the placeholder (A 750k, B 650k, C 550k, D 450k) and correct from the owner's tank
   runs? Recommended: yes — derived at read time, so a fix is retroactive.
2. **Jackets for the ~45 RISE-only songs.** The only source is the game's own asset bundles (per-song preview
   images, plainly named). Extract them, as the site already mirrors piugame's art? Recommended: yes.
3. **Rise single difficulty display.** CSS chips in the game's red/blue badge style (recommended, no assets), or
   reuse the Phoenix 2 stepball art?
4. **Both mixes top-level in the picker?** Recommended: yes.
5. **Widen the v1 score POST to the RISE mixes in phase 1** so the capture app has its endpoint on day one?
   Recommended: yes; it is a parser change and a golden file.
6. **Rise Arcade palette.** Its own, derived from the Arcade Station's screens (recommended), or share Phoenix's?

Not blocking, still open: the grey-grade rule (§5.5); whether Rise Arcade follows Phoenix 2 where Phoenix 2 added a
chart to an existing song.

---

## 10. Evidence and sources

- Owner's in-game checks (2026-09-21): records do not cross stations; hold heads are notes in RISE mode and not in
  the Arcade Station; the Arcade Station counters (269 → 352); 40 screenshots, 18 distinct result screens
  (`2026-09-21/rise-result-screens.csv`).
- [新・Pump It Up Wiki, RISE](https://wikiwiki.jp/piujpn/Pump%20It%20Up%20RISE) — the rank table, the grey-grade
  rule, hold judgment, the changed hold sections.
- [namu wiki, 펌프 잇 업 RISE](https://namu.wiki/w/%ED%8E%8C%ED%94%84%20%EC%9E%87%20%EC%97%85%20RISE) and its
  game-information subpage — the mark definitions, break off / break on, hold ticks; the developer Q&A (A9: the hold
  system "is a unique system for RISE" with no plans to change).
- [Steam patch notes](https://store.steampowered.com/news/app/2756930) via the news API — versions, song tables,
  the Arcade Station (1.4.0), the 0.5.1 HP rebalance.
- The community sheets (§4.1, §4.2); the Steam DLC store pages; the Pump Pro+ launch PDF.
- The game install: `resources.assets` / `sharedassets0.assets` (grade, mark and Arcade Station sprites, readable
  with UnityPy); the IL2CPP string table (server hosts, AES); Steam Cloud saves (encrypted); the in-game guidebook
  images under `StreamingAssets/Guidebook/`.
