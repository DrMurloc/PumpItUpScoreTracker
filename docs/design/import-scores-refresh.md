# Import Scores page refresh

The `/UploadPhoenixScores` page, brought up to the current design system. The home-dashboard
Import widget ([import-widget-remember-password.md](import-widget-remember-password.md)) is the
primary import path now; this page is the full-control form — every option, plus the manual
script/CSV fallback. The credential flows (store / stored / typed one-time) are the widget doc's
§Flows and are unchanged here.

Mock (2026-07-16 workshop, owner-approved): phone + desktop frames, running state, expander —
the copy deck below is the mock's copy verbatim.

## Decisions

- **D1 — Form first.** The page's job is "type credentials, press Import." The old four
  paragraphs of methodology pushed the password field to the fold line and the Import button a
  full screen below it at 390×812 (UX rule 1). One lede line survives; every other fact moves
  into helper text on the control it describes (rule 2's footnote clause: the password-storage
  promise and the broken-scores behavior were hover-tooltips, unreachable on touch).
- **D2 — One page, one mode.** The `Step` mode machine stops switching the whole page between
  password and script "steps." The password form *is* the page; `Step` shrinks to the CSV
  pipeline's own states (Uploading → Confirming → Saving → Finished) scoped inside the manual
  section. This structurally removes the old escape hatch where the footer toggle rendered
  mid-Saving and could swap the view while the save loop kept running.
- **D3 — Manual import is an expander, not a tab.** Owner call: the script/CSV flow serves
  technical users, not the primary case. It keeps everything it could do (copy script, page
  range, CSV upload, failures download) behind one labeled `MudExpansionPanel`, collapsed by
  default, footnoted "For re-importing older scores" — the official import self-limits its
  lookback (~5 pages of bests + recent plays for broken/weekly), so backfills are this flow's
  reason to exist.
- **D4 — Import rides the page dock on phones** (rule 10). The shell renders dock content
  `shell-mobile-only`, so the page keeps an inline Import in the card footer for desktop and
  hides it at phone widths — the ChartRandomizer split.
- **D5 — Results speak the system vocabulary** (rules 2/3/8). `SongImage` replaces the raw
  `MudImage`+tooltip; the "Ranking %" prose column ("97.5 of 12 Similar Players") dies —
  `ScoreBreakdown` already renders the ranking as rarity-colored score text with the localized
  percentile tooltip. No new component minted.
- **D6 — Script download is a Blob, not `window.open(data:)`.** Chrome has blocked top-frame
  `data:` navigation since 2017 — the true source of the script's "hacky, disable ad blockers"
  reputation. A Blob + `<a download>` click lands a real `piu-scores.csv`. Consequences:
  - The `#`→`Num` mangling existed only because `encodeURI` leaves `#` unescaped (truncating a
    data URI). It's gone; song names arrive intact. The parser's `Witch Doctor Num1` special
    case stays for old CSVs — back-compatible both ways.
  - UTF-8 BOM on the Blob so Excel opens Korean/Japanese song names cleanly (users edit these
    files to fix failed rows).
  - Signed-out detection (empty first page → alert instead of a silent empty CSV), per-row
    try/catch with an end-of-run summary, off-by-one page log fixed.
- **D7 — Every string through `L[…]`.** The page carried hardcoded field labels, table headers,
  snackbars, and status strings. New keys land in all nine locales in one pass; the dead keys
  (`Phoenix Import Info 1–4`, `Use Password 1–4`) are removed everywhere.
- **D8 — `IsImportGated` dies.** The widget-PR's C9 neutralized it to `false`; the two
  unreachable ComingSoon branches finally go with it, along with `_isScoreTableShowing`, the
  unused `SongMapping` record, and the `TimeSpan.MinValue` first-frame garbage in the Saving
  progress line.

## Copy deck (en)

| Where | Copy |
|---|---|
| Lede | PIU Scores signs into your piugame.com account and imports your new and improved scores. Once it starts, you can leave — it finishes in the background. |
| Password helper | Used to sign in, never stored — unless you turn on Remember below. |
| Broken-scores helper | Also imports fails from your recent plays; passing scores replace them later. |
| PIU Tracker option | Also send scores to piutracker.app |
| PIU Tracker privacy warning | PIU Tracker is fully public — scores sent there are visible to everyone. |
| Manual expander label | Manual import — console script + CSV |
| Manual footnote | For re-importing older scores |
| Step 1 | Choose which pages of your best scores to pull. Leave To page empty for all of them. |
| Step 2 | Copy the script. |
| Step 3 | While logged in on phoenix.piugame.com, paste it into the browser console (F12) — it downloads a CSV of your scores. Ad blockers can break it. |
| Step 4 | Upload the CSV here — also takes a hand-kept spreadsheet with the same columns. |
| Skeleton caption | Imported scores will appear here as they come in. |
| Confirming | Only new or improved scores will be saved. Saving can't be undone — stopping midway keeps what's already saved. |
| Saving | Leaving or cancelling stops the upload; scores already saved stay. |
| Failures | Some rows couldn't be imported. Download the failures, fix them, and re-upload. |

Voice: second person, "PIU Scores" (with the space), no first-person "I", no apologies.

## Constraints that must hold

- The E2E `ScoreImportTests` pins: the `PIUGame.com Username` / `PIUGame.com Password` field
  labels, the exact accessible name "Import", the Game Card control appearing after the
  multi-card first click, and streamed score text in the results. The refresh preserves all
  four (en strings unchanged; they only gain `L[…]` wrappers).
- The route stays `/UploadPhoenixScores` (the Import widget and shell menus link it); the
  user-facing title becomes "Import Scores", matching the nav and the widget.
- The CSV wire shape (`Song,Difficulty,Score,LetterGrade,Plate`) and
  `PhoenixScoreFileExtractor` are untouched.

**Amended 2026-07-30 ([score-truth-model.md](score-truth-model.md)):** two copy changes ride
with the truth-model work.

- The broken-scores checkbox is **"Record broken scores as your best"**, not "Include Broken
  Scores" — the old label said what was fetched, not what it did to your records. Its helper
  states the rule the label cannot: *"On charts you've never passed, your best broken attempt
  becomes your record. A pass always wins."*

  **Amended 2026-08-10.** The box is now backed by an account-wide preference instead of being
  re-derived from the mix on every load ([score-truth-model.md D2a](score-truth-model.md)), and
  the helper splits per mix so it can say *why* it defaults where it does — Phoenix 2 keeps a
  personal best for broken scores and Phoenix does not.

  It does **not** define what a broken score is (owner, 2026-08-11). A draft opened with "a broken
  score is a run that failed the stage"; anyone reading an import option on this site already
  knows, and the sentence spent the caption's first line teaching them nothing.

  **Amended 2026-08-17 ([stage-breaks-and-max-combo.md](stage-breaks-and-max-combo.md)).** All
  three helper variants end with one more sentence: *"A stage break — a run that ended before the
  last note — is never your record either way; it stays in your play history."* It is the one
  definition the caption carries, kept because it draws the line the option itself stops at: the
  box governs failed-and-finished runs, and a player who has just seen a 900k number vanish from
  their bests needs to know a stage break was never eligible, on or off.

  The tools line beneath it names PIU Tracker as a link out (new tab), alongside the existing
  Community Tools link. Both live inside one localized sentence rendered as markup — splitting it
  into fragments would assume an English word order that ja-JP and ko-KR do not share.
- **A CSV upload is a manual submission** (D9), so it is authoritative and may lower a record.
  The confirm step's "Only new or improved scores will be saved" was true and is not any more;
  it now reads *"Your file becomes your records, even where it scores lower."*

## Field-test rounds

- **R1 (2026-07-16)**: top spacing on the card; the results skeleton gets a naming caption
  (an unlabeled shimmer read as broken); the Remember checkbox renders only while there is a
  typed password to remember (the saved alert + Forget already carry the stored state); the
  manual steps reorder to page range → copy → console → upload, because the copied script
  embeds the page range.

## Test strategy

Component behavior lands in `Tests.Components` (bUnit, the lowest rung that catches it):
form-first render, the saved-credential variant swapping only the credential fields, the
expander collapsed by default, the importing state disabling the form, dock registration.
The existing E2E import workflow test keeps covering the wire-to-ledger path unchanged.

## Interrupted runs

`ImportOutcome.Interrupted` joined the strip's vocabulary with restart recovery
([import-restart-recovery.md](import-restart-recovery.md)). It reads as **Unfinished** rather than
"Couldn't finish" — a run the process abandoned failed at nothing — carries `Severity.Info`, and
says that everything already saved is in while whatever the run had not reached is still missing.

The state is written by the startup recovery pass, not by the run itself, so it only ever appears
on a run whose own `finally` never got to speak.
