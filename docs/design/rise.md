# Pump It Up RISE — two keyboard mixes

Status: **design and technical scope complete for phase 1 (§11), no code; the build starts on the owner's go.**
Researched 2026-09-14 → 2026-09-22 from the owner's install, his screenshots, two community sheets, two wikis and the
Steam patch notes; the owner took the high-level plan to two Rise players (Sneezle, Dave) on 2026-09-21 and their
answers are folded in; every open question of §9 was answered by 2026-09-22. Every decision below is the owner's
where marked; the rest are *decided unless he objects*. The research bundle (every source file, the exported game art, the
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
- **D11 (owner, 2026-09-21). The RISE grade ladder is the published one** (§5.2), shipped with the unmeasured low
  floors as a placeholder. Ingested letter grades (the capture app reads the grade off the result screen) are
  checked against the table and every disproof is recorded, the way Phoenix 2's floors were found. Safe because a
  grade is derived from the score at read time and never stored.
- **D12 (owner, 2026-09-21). Art.** RISE's own grade and mark art, exported from the game files, serves the Rise
  mix; Rise Arcade reuses the site's Phoenix art, which is what the Arcade Station itself draws. Rise singles use the
  **Phoenix 2 stepballs for now**; half-doubles draw the **H. DOUBLE stepball** (owner, 2026-09-22, picked from the mock): piugame's own
  Phoenix 2 half-double layers — `hd_bg.png` / `hd_text.png`, the RISE MIX channel's, blue on the arcade — composed
  the way the site's flattened stepballs are (ring at 0.98, word at 1.54, digits at 1.2, fitted against the CDN's
  `d20.png`) and shifted onto the Pro series' Half-Double violet (hue 258); 25 files, `difficulty/Phoenix2/hdb4.png`
  … `hdb28.png`, uploaded and verified 2026-09-22. Rise Arcade has no half-doubles. Each mix gets its own palette, mocked before build.
- **D13 (owner, 2026-09-21). Both mixes are top-level in the picker.**
- **D14 (owner, 2026-09-22). The score endpoint is a v2 write for observed plays** (§6.3), specified in phase 1 so
  the capture app builds against a fixed contract; v1 stays frozen.
- **D16 (owner, 2026-09-22). Jackets are the rectangular key visuals, uncut.** The game's 1920×1080 stills as they
  are — never squares, never resized; the card aesthetics are not being redone for tiny squares.
- **D15 (owner, 2026-09-22). The two palettes are approved as mocked** (round 1: Rise = R!SE yellow primary with
  hot pink and cyan on ink-navy; Rise Arcade = aqua primary, lavender accent and the shared yellow on indigo).

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
| `ApiMixParser` (v1) | Phoenix, Phoenix2 only | untouched — v1 is frozen; the v2 surface takes both mixes (D14, §6.3) |

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
- **Songs new to the tracker:** 50 (RISE Vol.1 originals not already in the arcade, all 20 Vol.2, the contest five,
  a few Variety crossovers, a few spellings). They need Song rows with artist and BPM (both in hand) and images.
- **Images.** The game keeps three per song, keyed by an internal id rather than a title: an **88×88 snippet** (what
  the wheel draws), a **576×324 eyecatch** and a **1920×1080 still** (the preview panel). All three come from one 16:9
  key visual; the official BGA video's thumbnail is the same visual with the title laid over it, at 480×360. **That
  key visual is the jacket** (D16): piugame's own jackets are 700×393 rectangles of the same visuals with the title
  added, the cards draw jackets with `object-fit: cover`, and the RISE stills go up **untouched at 1920×1080**
  (`2026-09-22-jackets/`, one file per song under the `songs/` naming rule — `Name.Where(IsAsciiLetterOrDigit)` —
  plus a manifest). **All 50 songs new to the tracker are named, filed and uploaded to the CDN** (2026-09-22, from the owner's RISE,
  REMIX and VARIETY channel screenshots, every thumbnail match unique; `songs/<name>.png`, `image/png`,
  create-only, verified by read-back). `title-to-id.json` in the bundle is the
  title → game-id map for every song seen on a wheel, which is also what a RISE new-song batch will need. The ids are the arcade's own song codes for arcade songs (`b29`, `e928`,
  `18d0`) and a 10001+ block for RISE-era songs; matching the wheel thumbnails in the owner's channel screenshots
  against the 88×88 snippets named all 48 RISE Vol.1 and Vol.2 songs plus Into the PIUniverse! (`title-to-id.json`
  in the bundle; every match unique, no conflicts). The contest five still need one CONTEST-channel screenshot.
  Which asset the `songs/` folder gets is the owner's call (§9 Q1).

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

Rules from the owner (2026-09-21): charts keep their order across mixes (an order swap has happened twice in the
series' history); a re-rate moves one level, rarely two, never three; RISE was cut from the Phoenix 1 catalog, so
Phoenix 2's later re-rates are re-rates of RISE charts too; a RISE single that matches nothing arcade within two
levels is RISE-only. The mapper (`mapper.py` in the bundle) is an order-preserving alignment of each song's RISE
single levels onto its arcade charts, each arcade chart carrying its Phoenix 1 level and its Phoenix 2 level:

| RISE single | Treatment | Count |
|---|---|---|
| at the chart's Phoenix 1 level | membership row on that chart | 1,448 |
| at a level a chart Phoenix 2 added | membership row on that chart | 56 |
| one level off the chart, Phoenix 2 later made the same move | membership row; RISE re-rated first | 13 |
| one level off the chart, Phoenix 2 did not follow | membership row at RISE's level | 54 |
| no arcade single within two levels | new chart row, `OriginalMix = Rise` | 16 |
| arcade singles RISE does not carry | nothing | 34 |
| singles on songs the tracker lacks | new song + new chart rows | 216 on 50 songs |

Only a song whose alignment is ambiguous (two equally good alignments, a two-level shift, or a RISE-only chart sitting
beside an arcade chart another RISE chart already took) went to the owner: 5 songs, 17 charts (`2026-09-21/
rise-singles-review-v2.xlsx`; the second tab is every automatic decision with its reading). **Reviewed and confirmed
in-game 2026-09-22 — every reading held** (Like Me S16 is the arcade S14 moved two; Sorceress Elise S18/S19 are the
17 and 18 moved one and its S21 is gone; Close Your Eye S8/S17 and Hello S19 are RISE-only; L (PIU Edit) S10 is the
arcade S12 moved two). No mapping is outstanding. Every half-double is a new chart row regardless (D7).

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

### 6.3 API — a v2 write for observed plays (D14)

Two ways to give the phase-2 capture app somewhere to post.

**Widen v1.** `POST api/phoenixScores` (song, type, level, score, plate, broken, mix; `KeepBestStats`) exists, and
its `ApiMixParser` admits only Phoenix and Phoenix 2 by design; admitting every Phoenix-scored mix is a parser change
and an additive golden. Against it: v1 is frozen and Phoenix-shaped. Its mix *defaults to Phoenix 1*, so a client
that forgets the field writes Phoenix 1 records with no error (the hazard behind
`mix-defaults-to-phoenix-on-contracts`). It takes score, plate and broken only, so the five judgments, max combo,
accuracy and the grade the screen showed are dropped at the door, and D10 (learned note counts) and D11 (recorded
grade disproofs) both need exactly those. And it records a *best*, not a *play*, so the journal gets no judged entry
and the session cards have nothing to draw.

**A v2 write for observed plays.** v2 already requires the mix on every call and publishes `scoringModel`; it has no
write endpoint today. `POST api/v2/players/me/plays` would take mix, chart (song + type + level, or chart id), the
five judgments, max combo, score, accuracy as shown, broken, the grade and mark as shown, `observedAt` and a `source`
(screen grab, screenshot). The server recomputes the score from the judgments and rejects a capture that does not
reconcile, the checksum the arcade-photo extractor used. It lands on ScoreLedger's existing
`RecordObservedPlaysCommand` (judged plays into the journal, idempotent on play time), plus the best-attempt policy
for a play that beats the record, so it is a controller and a golden rather than a new pipeline. Against it: a new
public surface to pin in `Tests.Api`, and one more thing partner tools can call, which is also the point: a future
photo extractor or partner tool posts plays the same way.

Decided (owner, 2026-09-22): the v2 write, specified now so phase 2 builds against a fixed contract; v1 stays frozen.
Manual entry and spreadsheet upload need neither.

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
- **Difficulty:** half-doubles draw the H. DOUBLE stepball (D12). Rise singles reuse the Phoenix 2 stepballs
  for now (D12); Rise Arcade reuses them too, which is what the station draws.
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

### 8.0 What phase 1 delivers, feature by feature

The reference list the owner asked for (2026-09-22). "Works" means on both new mixes unless a column says otherwise.

| Area | At the end of phase 1 |
|---|---|
| Mix picker | **Rise** and **Rise Arcade** beside the three primaries, each with its own theme (D15) and pill; the anonymous mix cookie, the account default and the shell seed all accept them |
| Catalog, Rise | every song on the community sheet (421 on 2026-09-21) with its 5K Single and 6K Half-Double charts at Rise's levels, each stamped with the Rise patch it arrived in; 50 songs new to the site with their jackets (uploaded 2026-09-22) |
| Catalog, Rise Arcade | the 352-song Arcade Station list (§4.2) as membership rows on the Phoenix 2 charts, Single and Double, Phoenix 2 levels and note counts |
| Chart pages and search | `/Charts` browse and search, the canonical chart page, the details dialog and the app-bar search on both mixes; half-doubles draw the H. DOUBLE stepball, singles the Phoenix 2 stepballs (D12) |
| Recording | manual entry on the chart page, the details dialog, the SRP quick record and the Quick Record widget; Rise grades with no plus tiers on the published ladder (§5.2); Rise's three marks as its plates, shown as Perfect Game / Full Combo / No Miss (D5); Rise Arcade with Phoenix 2 grades and the eight plates; the broken flag on both |
| Spreadsheet upload | one upload page for both mixes: Song, Difficulty (`S16`, `HDB23`, `D20`), Score, Plate or mark (`PG`/`FC`/`NM` accepted), IsBroken; keep-best by default |
| Score art | Rise letters, broken letters and mark badges from the game (D12) with a per-mix art path; Rise Arcade draws the site's Phoenix set |
| Passed-in-another-mix border | on, but only within a platform: Rise ↔ Rise Arcade, and the arcade family among themselves (D6) |
| Tier lists | open on both mixes with community votes; the score-derived lenses fill in as scores accumulate |
| Player page, journal, sessions | records and the score journal on both mixes; the rating tiles, PUMBILITY and official standing hidden rather than drawn as zeros |
| API | `GET api/v2/mixes` lists both with `scoringModel: phoenix`; every v2 read takes them; **`POST api/v2/players/me/plays`** (D14) accepts a judged play with the score checksum, the capture app's endpoint; v1 unchanged |
| Off the nav | PUMBILITY, the recap, Titles, Weekly Charts, March of Murlocs, the Leaderboards group and the two Phoenix calculators are not offered on either Rise mix (owner, 2026-09-22); reached by URL they explain themselves, as on a legacy mix — no route gate (§11.2, `MixCapabilities`) |
| Not in phase 1 | leaderboards and PUMBILITY tuning (phase 3), the capture app (phase 2), Discord announcements for the new mixes, `/Admin/BulkAddCharts` learning a mix (future Rise songs arrive through the catalog tool's SQL until then) |
| Docs and locales | DOMAIN.md, API.md, UX-GUIDELINES.md, DATABASE-SCHEMA.md updated; every new string in all nine locales |

### 8.1 Phase-1 build order (proposed, for the technical-scope pass)

The commit series, each green on its own, docs first and localization last, one PR:

1. **Profile.** `MixProfile` + `MixProfiles.For(mix)` in SharedKernel with a DomainTest that every enum value has
   one; `UsesLegacyScoring()`, `MixCapabilities.*`, `LetterGradeFor`/`GetMinimumScoreFor`/`GetMaximumScoreFor` become
   reads off it; the dozen wrong-question call sites of §3 move to the right field (official site, art, platform,
   lifebar). No behavior change for the 30 existing mixes — the test is the existing suites staying green.
2. **The two mixes.** `MixEnum.Rise`, `MixEnum.RiseArcade`, their `MixIds` Guids, one migration seeding the two
   `scores.Mix` rows (SortOrder after Phoenix 2, both primary), their profiles, the Rise floors table and the
   RiseMarks award vocabulary with display names.
3. **Theme and art.** Two `MixPalette`s, `ThemedMixes`, the CSS classes, per-mix letter and plate paths in
   `ShareCardImages` (`letters/{mix}/…`, `plates/{mix}/…`, flat fallback), the owner uploads the Rise set and the
   50 jackets.
4. **Catalog tooling.** `tools/RiseCatalog/` (the `PumpoutExtractor` precedent): reads the sheet snapshot, Dave's
   list, the site's Phoenix exports and `title-to-id.json`; emits idempotent SQL to Downloads — Rise songs, charts,
   membership rows and `MixVersion` rows; Rise Arcade membership rows on the Phoenix 2 charts. Bulk data never
   enters the repo.
5. **Recording.** The upload page driven by the profile (site-less Phoenix mix = spreadsheet flow alone), the
   RISE mark shorthand in the extractor, the profile's award list in `RecordScoreForm`.
6. **The v2 write.** `POST api/v2/players/me/plays` on `RecordObservedPlaysCommand` + the best-attempt policy, the
   judgment checksum, a `Tests.Api` golden.
7. **Docs and locales.** DOMAIN.md (RISE terms), API.md, UX-GUIDELINES.md, DATABASE-SCHEMA.md (the two rows),
   this doc's status; new strings in all nine locales.

**Phase 2 — the capture app**: screen-grab and F12 modes, template-matched
digits on the fixed result layout, the score and accuracy checksums, chart resolution against the Rise catalog,
`POST api/phoenixScores`. Skips Challenge aggregates (division badge in the header). Learns note counts (D10).

**Phase 3 — boards and PUMBILITY** with their own tuning; revisit the capability answers.

---

## 9. Open questions (phase 1)

None outstanding. Answered 2026-09-21/22: low floors ship as a placeholder and disproofs are recorded (D11); Rise
singles use Phoenix 2 stepballs (D12); both mixes are top-level (D13); the v2 observed-plays write (D14); the
palettes (D15); uncut key visuals as jackets (D16); the singles mapping confirmed in-game (§4.3); every new song's
image named (§4.1).

Not blocking, still open, for phase 2 or later: the grey-grade rule (§5.5), which the capture app's disproof log will
settle for free; whether Rise Arcade follows Phoenix 2 where Phoenix 2 added a chart to an existing song; the sheet's
"Pop & Pump & FIVE!!" spelling (the game and Steam say "DIVE!!" — the catalog uses the game's).

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

---

## 11. Technical scope (phase 1)

Written 2026-09-22 against the code at `b9feba3b`; every file named here was read. The build order is §8.1; this is
what each step touches, by project. One PR, docs first, locales last. **No new vertical, no new table, no new bus
message, no new recurring job.**

### 11.1 The shape of the change

A new mix is data plus one profile row — that is the point of `MixProfile` (§3) — and most of phase 1 is making
that sentence true. Today some thirty places spell "Phoenix or Phoenix 2" by hand, and each is a place a Rise
player gets a wrong answer or a crash. They come in four kinds:

| Kind | What happens on Rise today | Fix |
|---|---|---|
| A boolean asked the wrong question (`UsesLegacyScoring()`) | Rise is Phoenix-scored, so every "not legacy" branch fires: the nav offers PUMBILITY, the import page asks for piugame credentials, the bubble path is `difficulty/Rise/s16.png` (404), the cross-mix border unions the pad mixes | the profile fields (§3 table) |
| An explicit mix list | `RecurringJobRunner` rebuilds tier lists for Phoenix and Phoenix 2 only; `WidgetRegistry` offers each widget to those two; `WipeUserScoresHandler.ParallelMixes`; `ThemedMixes`; `StaticHeadResolver` | add the two mixes where the feature works; leave the list alone where it does not |
| A switch that throws | `ScoringConfiguration.PumbilityScoring(mix)` throws for any mix but the two Phoenix ones, and the rating step of `HighlightCaptureSaga` reaches it on every score event | gate the callers on `HasPumbility()`; the throw stays, it is right |
| A switch with a safe default | `TitleLadders.For` → `NeverExisted`, `TitleLists.HasDifficultyTitles` → false, `PumbilityPoolBands.For` → empty, `GetAccentColor` → grey, `V2MixParser` → any enum name | nothing, or a Rise case where the default is wrong (the accent) |

### 11.2 By project

**SharedKernel** — where the two mixes become real.

- `Enums/MixEnum.cs`: `Rise` (`[Description("Rise")]`) and `RiseArcade` (`[Description("Rise Arcade")]`; `GetName()`
  feeds the picker, the chart-page slug `rise-arcade` and the mix cookie); `IsPrimary()` includes both (D13);
  `DisplayOrder()` 290 / 300; `GetAccentColor()` two cases from the palettes (D15); `UsesLegacyScoring()` becomes
  `MixProfiles.For(mix).ScoringModel == ScoringModel.Legacy`.
- NEW `Enums/MixProfile.cs`: `sealed record MixProfile(ScoringModel, GradeLadder, AwardSet, OfficialSite, Platform,
  LifebarModel, IReadOnlyList<ChartType> ChartTypes, ArtSet Art)` and `MixProfiles.For(MixEnum)` — a dictionary over
  all 33 values, the §3 table verbatim. Rise: Phoenix · **Rise** · **RiseMarks** · None · Keyboard · None ·
  {Single, HalfDouble} · Rise letters on Phoenix 2 stepballs. Rise Arcade: Phoenix · Phoenix2 · PhoenixPlates ·
  None · Keyboard · None · {Single, Double} · Phoenix 2 everything.
- `Enums/MixCapabilities.cs`: the six flags read the profile. Phase-1 answers for both Rise mixes: `HasPumbility`,
  `HasRecap`, `HasOfficialBoards`, `HasWeeklyBoard`, `HasMarchOfMurlocs`, `HasPhoenixCalculators` all **false**.
  That is the entire "off the nav" rule: `ShellNav` and `ShellMoreSheet` already gate on these plus
  `TitleLadders.HasLadder`, so on Rise the menu is Home · Tier Lists · Charts · Import Scores · My Sessions · Rivals ·
  Chart Randomizer · Community · Lifebar Calculator · Mix Changes · About. The lifebar calculator and the mix diff
  stay because neither reads the selected mix (they stand on legacy mixes too). A page reached by URL anyway
  explains itself the way it does on a legacy mix; no route gate comes back.
- `Enums/PhoenixLetterGrade.cs`: `GradeLadder { Phoenix1, Phoenix2, Rise }`; a `RiseFloors` table (§5.2 — nine
  grades, no plus tiers, the placeholder low floors of D11); `LetterGradeFor` / `GetMinimumScoreFor` /
  `GetMaximumScoreFor` pick the table off the profile instead of `mix == Phoenix2`. `RecapPlayerTypeCalculator` and
  the calculator models keep their Phoenix tables (both features are off).
- `Enums/PhoenixPlate.cs` and its helpers: `AwardSet { PhoenixPlates, RiseMarks, None }`; `RiseMarks` = `PerfectGame`,
  `UltimateGame`, `SuperbGame` shown as Perfect Game / Full Combo / No Miss with shorthands `PG` / `FC` / `NM` (D5);
  `GetName(mix)` and the shorthand parse read the profile.
- `DomainTests`: every `MixEnum` value has a profile; the 31 existing mixes answer `UsesLegacyScoring`, `IsPrimary` and
  every capability flag exactly as before (a table test — the regression net for the refactor); the Rise ladder at
  every boundary and the absence of plus grades; mark parsing round-trips.

**Domain** — no change. `TitleLists`, `ScoreProjector` and the Phoenix 2 title classes never see the new mixes.

**Data**

- `Persistence/MixIds.cs`: two Guids, minted once, hardcoded here, in the migration and in the tool's `MixMap` (the
  PumpoutExtractor rule).
- `Migrations/<stamp>_RiseMixes.cs`: `IF NOT EXISTS … INSERT [scores].[Mix]` for both rows (`Rise`, `RiseArcade` —
  `Name` is `MaxLength(10)`, exactly ten), SortOrder 290 / 300, IsPrimary 1 — the `LegacyMixCatalog` pattern, data
  only, no model change.
- No new table, no new column: `ChartMix.NoteCount` is already nullable (D10 needs that), `Chart.Type` already stores
  `HalfDouble`, `MixVersion.Name` is `MaxLength(16)`.

**Catalog** — no code. `MixVersion` rows arrive by SQL (§11.3); `GetMixVersionsQuery`, `api/v2/versions`, the chart
slugs (`ChartSlugs.MixSlug` is `Slugify(GetName())`) and the `AddedInVersionId` stamps work unchanged.
`StepChartIngest` and `PiuCenterCrawlSaga` stay Phoenix-only by design.

**ScoreLedger**

- `WipeUserScoresHandler.ParallelMixes` → every Phoenix-scored mix from the profile (Your Data must delete a Rise
  record too).
- `GetCrossMixPassesHandler` → every Phoenix-scored mix **on the viewer's platform** (D6: Rise ↔ Rise Arcade, the pad
  family among themselves, never across).
- `BackfillMaxCombosConsumer`, `BrokenRecordCleanupSaga` → read `ScoringModel` (same effect, right question);
  `BackfillStageBreakCausesConsumer` → mixes with a lifebar model, so never Rise (D9).
- `RecordObservedPlaysCommand` is the v2 write's target, unchanged; keep-best is the ledger's existing policy.

**PlayerProgress**

- The rating step of `HighlightCaptureSaga` and the player-stats recompute skip a mix whose `HasPumbility()` is false
  before `PumbilityScoring` is reached; today a Rise score event would throw inside a failure-isolated step and log
  an error per session.
- `TitleLadders.For` already answers `NeverExisted` for both, so Titles is off the nav and the page says so.

**ChartIntelligence** — no code. `TierListSaga`, the scoring-difficulty and letter-difficulty lenses, similarity and
community votes are per-mix already; what changes is who asks them to run (`RecurringJobRunner`, below). Hardmode,
the PUMBILITY tier list and the folders stay Phoenix 2.

**OfficialMirror** — `PiuGameConfiguration.BaseUrlFor` reads `OfficialSite` and answers null for a site-less mix; its
callers already sit behind the import page and the leaderboard job, neither of which reaches Rise.
`GetChartTypeFromUrl` keeps skipping `hd` (D8).

**Communities, Rivals, CommunityTools, EventCompetition, WeeklyChallenge, Seasons, Identity, HomePage, Translations,
Randomizer** — no change. The randomizer draws from the selected mix's charts and just works; the Discord bot's
Phoenix 2 default and the role table are Phoenix 2 by design; peers and rivals read the score reader, which is per
mix.

**Web**

- Theme — `Services/Theming/MixThemes.cs`: two `MixPalette`s from the approved mocks (D15) with their hue and rarity
  ramps, `ThemedMixes` += both, `CssClassFor` → `theme-rise` / `theme-rise-arcade`. Nothing in `site.css` keys on a
  theme class; no stylesheet change.
- Art — `Services/ShareCardImages.cs`: `DifficultyBubble` → `difficulty/{profile.Art}/…` (both Rise mixes answer
  `Phoenix2`: the singles and doubles are the Phoenix 2 stepballs, D12, and the 25 half-double files are there);
  `LetterGrade` → `letters/Rise/{grade}.png` and `_broken` on Rise, the flat set elsewhere; `Plate` →
  `plates/Rise/{pg|ug|sg}.png` on Rise. `Components/DifficultyBubble.razor`: the half-double chip only where the art
  set has no half-double bubble (Infinity keeps its chip). `LetterGradeIcon` is unchanged; it reads `ShareCardImages`.
- Shell — nothing. `ShellMixMenu` lists `IsPrimary()` by `DisplayOrder()`, the nav gates on the flags,
  `ShellModelFactory` parses the cookie by enum name.
- Recording — `Components/RecordScoreForm.razor`: the award picker from `profile.Awards`;
  `Services/PhoenixScoreFileExtractor.cs`: `PG` / `FC` / `NM` beside the plate codes (`HDB23` already parses);
  `Pages/UploadPhoenixScores.razor`: a site-less Phoenix mix gets the spreadsheet flow alone — no credential fields,
  no piugame session; the copy is the owner's.
- `Pages/Progress/Player.razor`: the PUMBILITY, rating and official-standing tiles hide behind `HasPumbility()` /
  `HasOfficialBoards()` instead of drawing zeros. `Services/ShareCardComposer.cs:179`: `mix is not (Phoenix or
  Phoenix2)` → `ScoringModel`, so a Rise share card carries its letter and mark.
- `HostedServices/RecurringJobRunner.cs`: `ProcessScoresTiersListCommand`, `RecalculateScoringDifficultyCommand`,
  `ProcessPassTierListCommand`, `RecalculateChartLetterDifficultiesCommand` and `RecalculateChartSimilarityCommand`
  each publish for the two mixes too (one line each); the weekly rotation, Daily Step, the PUMBILITY tier list,
  Hardmode and the leaderboard import do not. No new job, no SCHEDULED-JOBS row.
- `Services/HomeDashboard/WidgetRegistry.cs`: the two mixes on every widget whose data exists on them (quick record,
  import, account stats, by-level breakdown, sessions); not the PUMBILITY widget, not Daily Step (its rotation is
  off).
- `Services/StaticHeadResolver.cs`: Rise descriptions for the SEO head (two `mix is Phoenix or Phoenix2 ? mix :
  Phoenix` fallbacks); the copy is the owner's.
- API — `V2MixParser` parses by enum name, so every v2 read takes both mixes with no change. `GET api/v2/mixes` lists
  them with `scoringModel: phoenix`: the `Tests.Api` golden grows two rows, additive but a contract change, said so
  in the PR. NEW `POST api/v2/players/me/plays` on `PlayersController` (§6.3, D14): DTO in `Dtos/ApiV2/`, the
  judgment checksum validated at the boundary, dispatches `RecordObservedPlaysCommand`; a `Tests.Api` golden for the
  request and the 201 / 400 shapes; an API.md row. v1 `ApiMixParser` untouched.
- Localization: the three mark names, the upload page's Rise lines, and whatever a hidden page still lacks (most
  already carry their legacy-mix line) — all nine locales, alphabetical, the last commit.
- `Tests.Components`: a Rise half-double renders the image and an Infinity one the chip; `RecordScoreForm` offers
  three marks on Rise and eight plates on Rise Arcade; `ShellNav` on Rise offers none of the gated items; the upload
  page on Rise shows no credential fields.

### 11.3 The SQL — `tools/RiseCatalog/`

A console app beside `PumpoutExtractor`, under its rules: not in the solution, Sonar-excluded, output to Downloads,
reviewed and run by hand, nothing generated enters the repo. Inputs: a CSV export of the community sheet, Dave's
Arcade Station list, the site's `/Charts/Export.csv` for Phoenix and Phoenix 2 (or `export-prod-catalog.py` against
the local prod-synced database), `title-to-id.json`, and the Steam news patch dates. The order-preserving single
mapper (`mapper.py`, §4.3) ports over as `Matcher.cs` with the five confirmed readings baked in.

Four idempotent single-transaction scripts, `IF NOT EXISTS` per row, every Guid deterministic (v5 from
`mix|song|type|level`) so a re-run is a no-op:

| Script | Rows (2026-09-22 counts) | Needs |
|---|---|---|
| `s1-rise-versions.sql` | `scores.MixVersion`: the 14 Rise patches on the sheet (`Base`, `0.2.1` … `0.8.1`, `1.0.0` … `1.3.0`, `1.5.0`) plus `1.4.0`; Rise Arcade `1.4.0` and later; dated from the Steam notices | the Rise mixes migration |
| `s2-rise-songs-charts.sql` | `scores.Song`: 50 (artist and BPM from the sheet, `Type` from the channel, `ImagePath` the jackets already on the CDN, `Duration` measured by the tool from the game's audio clips, `00:00` where it cannot); `scores.Chart`: 16 Rise-only singles on existing songs + 216 singles on the new songs + 1,173 half-doubles, `OriginalMixId = Rise`, `StepArtist` NULL | s1 |
| `s3-rise-membership.sql` | `scores.ChartMix` for Rise: ≈1,800 singles (1,571 onto existing arcade charts at Rise's level, the rest onto s2's rows) + 1,173 half-doubles; `NoteCount` NULL (D10); `AddedInVersionId` = the song's arrival patch | s2 |
| `s4-rise-arcade-membership.sql` | `scores.ChartMix` for Rise Arcade: 2,385 rows (1,472 S + 913 D) onto the Phoenix 2 charts of the 352 songs, Phoenix 2 level and note count copied; `AddedInVersionId` = `1.4.0` unless a later notice added the song | s1 |

Plus `reports/`: the alignment sheet (every automatic reading), unmatched names, and `art-needed.txt` (expected
empty — the 50 are up).

### 11.4 Docs, in the first commit

This doc's status; DOMAIN.md (Rise, Rise Arcade, half-double, the marks); API.md (the mixes list, the plays write);
UX-GUIDELINES.md (two palettes); DATABASE-SCHEMA.md (`scores.Mix` "33 mixes", the MixVersion note); CLAUDE.md, one
line under Domain models: a new mix is a profile row, an enum value, a `MixIds` Guid and a `scores.Mix` row.

### 11.5 Owner-owed before the PR merges

- The 59 sprites, now with paths: `letters/Rise/<grade>.png` and `letters/Rise/<grade>_broken.png` for `sss ss s aa a
  b c d f`; `plates/Rise/pg.png`, `ug.png`, `sg.png` (shown as Perfect Game / Full Combo / No Miss).
- The four scripts against prod after the migration deploys, in order.
- The SEO descriptions and the upload page's Rise copy.
