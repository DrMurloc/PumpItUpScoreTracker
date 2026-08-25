# Chart video sourcing

Where chart videos come from and how they get written: which footage may represent a chart,
which channels are trusted, how a video is matched to a chart identity, and the process for
bulk backfills. Companion to [video-sides.md](video-sides.md) (the sides model and the dialog
UI — sides are durable data with three writers; this doc governs the "bulk audits assert via
SQL" writer) and [new-charts-json.md](new-charts-json.md) (registration-time videos for new
charts). Rulings dated 2026-08-25 unless noted.

## Coverage state and the program (2026-08-25)

8,396 charts; every video in the DB belonged to a chart alive on XX/Phoenix/Phoenix 2 until
this program started — the legacy buckets were 100% bare. Missing counts, bucketed by the
newest mix each chart appears in (post `singles-video-fix-2026-08-24.sql`):

| Bucket (newest mix) | Charts missing | Distinct songs | Stage |
|---|---|---|---|
| XX / Phoenix / Phoenix 2 | 48 → **7** | — | **1 — script delivered 2026-08-25** |
| Prime, Prime 2 | 844 | 153 | 2 — next |
| Infinity | 1,531 (1,342 Infinity-only charts) | 273 | 3 |
| Fiesta, Fiesta EX, Fiesta 2 | 344 | 81 | 4 |
| NX–NXA, Zero, Exceed 1–2, Prex, Premiere | 68 | ~34 | 5 |

Stage order is owner-approved: modern first, then Prime era, then Infinity, then Fiesta, then
the deep end. Each stage ships as one data-only SQL script + report (see the process section);
what still can't be filled after a stage is reviewed with the owner once the list exists.
Realistic recovery estimate under the footage policy: 60–75% of the legacy gap.

Stage 1's deliverables: `Downloads\modern-video-backfill-2026-08-25.sql` +
`modern-video-backfill-report-2026-08-25.md` (41 fills, 7 sides pairs, 1 mislink repair).

## Footage policy (owner rulings)

Acceptance ladder — always take the highest rung that exists for the chart:

1. **Clean produced showcase / direct screen capture.** The Nevsister/Official house style, and
   direct captures like VKIM's — the game screen only, chart fully readable. A judgment overlay
   from a played run is fine; an all-miss autoplay recording (chart scrolls, nobody stepping)
   is equally fine.
2. **Screen-only camera footage** — a camera pointed at a cab monitor or screen, **nobody in
   frame**. The typical Fiesta-era Korean upload style. Acceptable where rung 1 doesn't exist.
3. **Official port footage** (Pump It Up Infinity itself, Prex 3 PC) is real game content and
   sits on whichever of the rungs above matches its recording style.
4. **Simulator footage** (StepF2, StepPXX, StepPrime…) is an **absolute last resort** — link it
   only when nothing else exists anywhere, and flag it in the report.
5. **Never gameplay/stream footage**: players in frame, facecams, stream VODs, tournament
   hand-cams. A "this is X chart" segment inside someone's play session does not qualify.

Mechanical stream filters when classifying at scale: video duration vs `Song.Duration` (a VOD
runs 30+ minutes), thumbnail inspection via `i.ytimg.com` (person/room vs clean screen), title
markers ("live", "stream", handcam). Sim footage often self-identifies with an on-screen
watermark (StepPXX prints one top-left) — thumbnails catch it.

## Trusted channels

Canonical `ChannelName` values are exact strings (column is `nvarchar(30)`); reuse them, never
invent variants. `NOT KNOWN` is the fallback for a verified-correct video whose channel isn't
on the roster.

| Channel | Id | Trust | Notes |
|---|---|---|---|
| `네브시스터NEVSISTER` | `UCicVRsgv4iIhZGZcbx7xUkw` | Wholesale (title-driven + spot checks) | 11K uploads. Series: XX (S/D/SP/DP/Co-Op), Phoenix + "Phoenix Modified ver.", Phoenix CO-OP X2, Phoenix 2, Prime 2, 20주년 anniversary. The default source everywhere their coverage reaches. |
| `PUMP IT UP Official` | `UC1zVbfSZSKz9r2AzF50l9sA` | Wholesale | Andamiro's channel; the check-new-charts watermark walk covers new uploads, but the backlog holds older-era videos never linked. |
| `VKIM` | `UCvoGVHCiHOlK8FQigiaUJyQ` (@vkimroyal) | Wholesale — he was one of the makers of Infinity | 5.1K uploads, direct capture, titles `Pump It Up <Mix> \| <Song> \| <difficulty (codes)>`. Series across Infinity, Pro, Pro 2, Fiesta, Fiesta EX, NX, NX2, NXA, Zero — including Half-Doubles (`HDB12`) and legacy Routine co-ops (`R16 (2P)`). **The key to the Infinity bucket.** Pro/Pro 2-labeled videos: see the identity rules below. |
| `BOSS_PIUVN Pump It Up Team` | `UCPq5v-pM3xWNvu00DGYxVAg` | Series trusted, per-video filter | 4.8K uploads. Produced title-card series (`✔` titles) across Fiesta EX/NX/Fiesta 2/Prime 2/XX/Phoenix — but the channel also posts stream videos, so classify each video, not the channel. |
| `Valius` | @valius1 | Per-video (established precedent row) | 7.7K, direct capture incl. all-miss autoplay chart visualizations. |
| JUNTROLL | `UCsMAD8y8KOBed6FKrbVZA8Q` | Per-video | 2.1K, direct capture, `[Pump It Up <Mix>] Song CODE` titles, Prime/Prime 2 strong. |
| ZELLLOOO | @MoreFlesh | Per-video — he's a streamer | 4.8K; his Fiesta EX chart videos are clean VS-screen captures, but vet each one. |
| TFDY | @TFDY-xd | Per-video, rung 2/3 | 224; Prex 3 PC series, camera-on-monitor. |
| Art Pump | — | Sim risk | StepPXX watermarks observed on "arcade" titles; treat as rung 4 unless a video proves otherwise. |

Naturenim, KOMERICA, mentormin, PURPLISM, taptapking and similar one-off uploaders are rung-2
fillers: acceptable per-video where nothing better exists, verified individually.

## Chart identity: which chart may a video land on

The rules that keep footage from landing on the wrong chart. All of them reduce to one
principle: **match on the chart's level for the mix the video was recorded on** (`ChartMix`
history), never on base `Level`, and never on song+level alone.

- **Era + level.** A video is a candidate for a chart only if the chart has a `ChartMix` row on
  the video's mix, at the level the title names. Nevsister relevel titles like
  `D13 (pre D11 → D13)` belong to the **modern** chart, not the legacy sibling that died at the
  old level.
- **Reintroduction ≠ same chart.** Same song at the same difficulty across a cut-and-return is
  not the same chart (owner's example: Gargoyle -FULL SONG- came back completely rewritten at
  the same difficulties). A `[PUMP IT UP PHOENIX]`-era video must never land on a chart whose
  newest mix is pre-XX.
- **Pro / Pro 2 / Infinity are one family** — the American release that did its own thing. The
  catalog models only Infinity (Pro and Pro 2 have no `ChartMix` rows), so a VKIM Pro-labeled
  video can only attach to an Infinity chart identity. Since chart-sameness across even that
  lineage is not guaranteed, Pro-labeled footage is an **in-family fallback only** — used where
  no Infinity-labeled video exists, and flagged distinctly in the report for owner review.
- **Arcade footage for the Infinity bucket** is valid only for the 189 charts that genuinely
  span an arcade mix (e.g. Crazy's Fiesta EX/Fiesta 2 charts carried into Infinity). The other
  1,342 are Infinity-only charts — fundamentally different steps from any arcade chart of the
  same song — and take Infinity/Pro-family footage or nothing.
- **Levelled legacy co-ops vs modern CO-OPx2.** Several ChartIds carry both an Infinity-era
  levelled Routine row and the modern x2 co-op identity (Chimera, LIADZ, LIADZ pt.2, Sorceress
  Elise, Tepris). Infinity Routine footage is presumed different steps — modern co-op rows take
  modern CO-OP videos only.
- **Substring song names are a standing hazard.** "Elise" ⊂ "Sorceress Elise" let Elise
  CO-OPx2 hold Sorceress Elise's video through a title audit; PICK ME ↔ Nekkoya and arcade ↔
  `- FULL SONG -` titles are the other recurring confusions. Matchers compare exact canonical
  names, never substrings.

## The backfill process

Each stage is **pure data — no code changes**. The pipeline:

1. **Targets** from the prod-synced local DB: charts with no `ChartVideo` row (or an empty
   URL), bucketed by newest mix.
2. **Discovery**, all quota-free: channel walks of the roster (the innertube machinery from
   `.claude/skills/check-new-charts/` generalized to arbitrary channel + full walk — walk once,
   cache watch pages, reuse across stages) plus results-page searches for the tail.
3. **Verification**: every candidate id is confirmed via keyless oEmbed
   (`youtube.com/oembed` — authoritative title + channel), with `i.ytimg.com` thumbnails for
   footage-tier classification and duration-vs-`Song.Duration` for stream filtering.
4. **Matching** per the identity rules above.
5. **The script**, one per stage in `Downloads`: a single transaction, `SET XACT_ABORT ON`,
   idempotent guarded writes (skip charts that already carry a video; delete empty-URL
   placeholder rows first), UTF-8 **with BOM** for SSMS, `N''` literals for Korean channel
   names. It must **refuse to commit** (THROW) if it would leave any of the invariants broken:
   - no URL held by charts of more than one song;
   - no URL held by more than two chart rows;
   - every two-row URL group sided exactly `Left`+`Right`, or both `NULL`.
   The invariant guard doubles as mislink detection — stage 1's caught the Elise row — and a
   collision with an existing wrong row is repaired in the same script, URL-guarded: repoint to
   the correct video if one exists, else clear to no-video (missing beats wrong,
   [video-sides.md](video-sides.md)).
6. **Sides** ([video-sides.md](video-sides.md) is the model): a video newly shared by two
   same-song singles-family charts gets sides written — lower level Left on the first modern
   mix carrying both (Phoenix 2 → Phoenix → XX), S+SP always S Left — **including the case
   where the new row joins a URL an existing sibling already holds**: both rows get sides, the
   sibling's update fires only when the join actually landed. Legacy-mix tie pairs stay `NULL`
   (unorderable). Doubles and co-ops never take sides.
7. **The report**, alongside the script: per-row chart / video id / verified title / channel /
   footage tier / side, the pair-joins, optional upgrades *not* taken, and the remains list.
   Tier per row is what lets the owner bulk-reject a rung.
8. **Validation before handoff**: a rolled-back dry run against the prod-synced local DB,
   asserting the invariants and the expected remaining-missing count.
9. **Application** (owner): run the script on prod **after** any pending video migrations and
   earlier scripts in the chain, then **/Admin → Clear Cache** — direct SQL bypasses the app,
   and chart videos cache for 14 days.

## No footage exists — DrMurloc can record these himself when he gets the time

Nothing on YouTube covers these seven (the six clears re-verified 2026-08-24, msgoon on
2026-08-25). All XX-era; any future upload is a one-row insert.

| Chart | Note |
|---|---|
| msgoon RMX pt.6 S7 | pt.1 S7 videos exist — different song |
| PICK ME S2 | |
| PICK ME S4 | |
| Turkey March -Minimal Tunes- SP2 | |
| Wedding Crashers SP4 | |
| Allegro Con Fuoco DP4 | |
| Sugar Conspiracy Theory S4 | |

## Open flags

- **Turkey March -Minimal Tunes- "Double 1"** (XX-only row): the catalog has no TM-MT
  DoublePerformance row, and the only level-1 double-pad chart that exists in reality is the
  DP1 performance chart — this row is almost certainly the DP1 chart typed `Double`. Stage 1
  linked the DP1 video (correct footage either way); retyping the chart row is a pending owner
  decision.
- **Optional pair-upgrades** stage 1 deliberately did not take (XX-era videos sitting on
  Phoenix-Modified charts whose partners now have the Phoenix-ver video) are listed in
  `modern-video-backfill-report-2026-08-25.md`.
