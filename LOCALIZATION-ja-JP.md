# ja-JP localization glossary

Working reference for translating `App.en-US.resx` into `App.ja-JP.resx`. Captures the conventions established by nabulator's seed translations (PR #44) and the machine-translation sweep that followed (PR #90), plus the deviations and known-bad strings a future native-speaker pass should clean up.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md#cross-cutting-concerns). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md). For the parallel pt-BR conventions, see [LOCALIZATION-pt-BR.md](LOCALIZATION-pt-BR.md).

## Style conventions

- **Polite register (です／ます).** ~60 entries use `〜ます／〜ません／〜です／〜ください`; only a handful of older nabulator strings drop into plain form (`変更できる`, `ロールバック出来ません` — see Known issues). New translations should be polite throughout. Avoid keigo (尊敬語/謙譲語) except in the few stock phrases that already use it (`ご連絡ください`).
- **Sentence case is not a thing in Japanese.** Ignore the en-US Title Case in keys; values just use natural Japanese without capitalization rules. (English brand names embedded mid-sentence — `Phoenix`, `PIU`, `Score Tracker`, `XX`, `Discord` — keep their original casing.)
- **Punctuation is full-width.** Established split: `（）` not `()`, `：` not `:`, `。` not `.` for sentence-final periods, `、` not `,` for in-sentence commas. Counts in the current resx: 31 full-width parens vs 3 half-width; 22 full-width `：` vs 2 half-width. Stay consistent.
- **Numerals are half-width** except in three legacy nabulator strings (`１レベル`, `１－４ポイント`). New translations use `1`, `2`, `10`, etc. Don't introduce more full-width numerals.
- **Preserve positional placeholders verbatim.** `{0}`, `{1}` go into the value untouched, in whatever order Japanese grammar wants. Example: key `Welcome to Score Tracker, X!` → value `Score Trackerへようこそ、{0}さん！`. The `さん` honorific suffix is appended after `{0}` because the placeholder is a name.
- **Skip prose with inline markup.** Per CLAUDE.md, `<MudText>` bodies with embedded `<MudLink>`/other elements stay hardcoded English. Don't extract them, don't translate them.
- **No `〜だ／である` written-style endings.** The app's voice is conversational-polite, not formal-written.

## Established term mappings

These have at least one existing translation in `App.ja-JP.resx`. New translations of the same term must reuse the established form unless there's a documented reason to change it.

### App / generic UI

| English | ja-JP | Notes |
|---|---|---|
| Score Tracker (the app) | Score Tracker | Brand name, kept English. Used inside Japanese sentences as-is. |
| Account | アカウント | |
| About | 概要 | "About The Site" → このサイトについて. |
| Actions | 操作 | |
| Add | 追加 | Suru-verb usage: `〜追加` for noun, `〜を追加` for verb objects. |
| Average | 平均 | |
| Cancel | キャンセル | |
| Close | 閉じる | |
| Confirm | 確認 | Also used for "Verification". |
| Completed | 完了 | Used for "Done" too. |
| Copy | コピー | "Copy Script" / "Copy to Clipboard" both → コピー. |
| Country | Country | **Untranslated** in nabulator's seed. See Known issues. |
| Create | 作成 | |
| Default | デフォルト | |
| Delete | 削除 | |
| Description | 説明 | |
| Difficulty | 難度 / 難しさ | Both forms appear; 難度 is preferred for compounds (`平均難度`, `難度カテゴリー分類`), 難しさ for the bare-level concept (`難しさレベル`). |
| Done | 完了 | |
| Download | ダウンロード | |
| Easy / Hard | 簡単 / 難しい | Plain adjective forms. |
| Easiest / Hardest Player | 下級プレーヤー / 上級プレーヤー | Note: rendered as "low-rank/high-rank" rather than literal -est. |
| Edit | 編集 | |
| Hide | 隠す | |
| Home | ホームページ | |
| Image | 画像 | "Upload Image" → 画像アップロード. |
| Language | 言語 | |
| Last Updated | 最近更新 | |
| Login | ログイン | |
| Logout | ログアウト | |
| Make Public / Private | 公開する / 非公開する | |
| Max / Min | 最大 / 最小 | "Max Level" → 最大レベル, "Min Score" → 最低スコア (注: 最低 is also used for ranges with a floor like Min BPM → 最低BPM). |
| Median | 中央値 | |
| Medium | 中 | |
| Name | 名前 | |
| Next | 次の | "Next Letter" → 次のランク. |
| Open | Open | **Untranslated** in nabulator's seed. See Known issues. |
| Page | ページ | "Starting Page" → 再開のページ, "Ending Page" → 最後のページ. |
| Pending | 未決定 | |
| Permissions | 権限 | |
| Place (rank, "1st place") | 場所 | |
| Public / Private | 公開 / 非公開 | |
| Reason | 理由 | |
| Recorded Date | 記録日 | |
| Recorded On X | {0}にき記録した | nabulator. Note "き" appears to be a typo before 記録した. |
| Restart | 再起動 | |
| Rules | 規則 | |
| Save | 保存 | |
| Search | 捜索 / 検索 | nabulator uses 捜索 ("Official Leaderboard Search" → 公開ランキング捜索); newer entries use 検索 ("Search User" → ユーザー検索). Pick one going forward — 検索 is the more common UI word. |
| Settings | 設定 | "Admin Settings" → 管理設定, "Extra Settings" → 追加設定. |
| Show | 表示 | "Show Age" → 歳表示, "Show Difficulty" → 難度表示. Suffix pattern. |
| Source | 出典 | |
| Source Code | ソースコード | |
| Start | 開始 | "Start Date" → 開始日, "Starting Life" → 開始体力. |
| Stats / Statistics | 統計 | "Player Stats" → 進捗中の譜面 (note: nabulator interpreted this as in-progress charts, not literal stats — see Known issues). "Game Stats" → ゲーム統計. |
| Submit | 登録 | Also used for "Submission". |
| Tag(s) | タグ | |
| Title (in-game title award) | タイトル | "Title Progress" → タイトル進捗, "Titles" → タイトル. |
| To Do | やりこと | **Typo** of やること. See Known issues. |
| Tools | ツルー | **Typo** of ツール. See Known issues. |
| Total | 総計 | |
| Type | 種類 | "Chart Type" → 譜面タイプ (Type rendered as タイプ here, but as 種類 elsewhere — context-dependent). |
| Ungraded / Not Graded | 未ランク / 未ランク数 | |
| Unknown | 不明 | |
| Update | 更新 | "Upload Scores" → スコア更新 (so 更新 doubles for "upload" in some contexts — see Upload below). |
| Upload | アップロード or 更新 | Inconsistent. "Upload Image" → 画像アップロード; "Upload Scores" → スコア更新; "アップロード" appears in disclaimers. Lean on context, prefer アップロード for new entries. |
| Username | ユーザ名 / ユーザー名 | nabulator uses ユーザ (no 長音); newer entries use ユーザー (with 長音). Inconsistent — see Known issues. |
| Validation | 確認 | |
| Very Easy / Very Hard | とても簡単 / とても難しい | |
| Video | ビデオ | "Video URL" → ビデオURL, "Open Video" → ビデオリンク開く. |
| Welcome | ようこそ | "Welcome to Score Tracker, X!" → Score Trackerへようこそ、{0}さん！ (placeholder takes さん). |

### PIU domain

| English | ja-JP | Notes |
|---|---|---|
| Chart(s) | 譜面 | nabulator's choice — translated, not loanword. Used compositionally: 譜面リスト, 譜面タイプ, 譜面作者, 譜面くじ引き, 譜面残り, 譜面比較. **Don't switch to チャート mid-file.** |
| CoOp | CoOp | Untranslated. "CoOp Aggregation" → CoOp集合 (see Known issues — 集合 is "set", not "aggregate"). |
| Co-Op | Co-Op | Distinct from "CoOp" — used as a select-item label on /WeeklyCharts. Both spellings appear in source verbatim. |
| Singles / Doubles | Singles / Doubles | Untranslated. "Singles Level" → Singlesレベル, "Doubles Level" → Doublesレベル. |
| Difficulty Level | 難しさレベル | |
| Letter Grade | ランク | Loanword, katakana. "Min/Max Letter Grade" → 最低/最高ランク. "Next Letter" → 次のランク. |
| Letter Difficulty | ランク難度 | |
| Mix | ベーション | **Typo** of バージョン. See Known issues. |
| Phoenix / XX | Phoenix / XX | Game versions, untranslated proper nouns. "Phoenix Score Calculator" → Phoenixスコア計算; "Upload XX Scores" → XXスコア更新. |
| Plate | プレート | Loanword, katakana. (Compare pt-BR which keeps it English.) |
| Pass (verb / noun for clearing a chart) | クリア | Semantic translation. "Hide Completed Charts" → 完了譜面隠す (uses 完了 for "completed"); "Passed Count" → クリア数; "Not Passed" → 未クリア数; "Stage Pass" → Stageクリア (Stage left English mid-word). |
| Score (singular) | スコア | "Score" → スコア, "Score State" → スコア状態, "My Score" → 自己スコア. |
| Scores (plural / collection) | スコア | Same word — Japanese doesn't grammatically distinguish. |
| Score (Data Backed) | スコア(データあり) | Half-width parens here are nabulator's choice — note inconsistency with the full-width-parens convention. |
| Note Count | ノーツ数 | "Min/Max Note Count" → 最低/最高ノーツ数. "Notes" → ノーツ. |
| Step Artist | 譜面作者 | "Step Artist: X" → 譜面作者：{0}. |
| BPM | BPM | Untranslated. "Min/Max BPM" → 最低/最高BPM. |
| Tier List | ティアリスト | Loanword, katakana. "PIU Tier List" → PIUティアリスト, "Calculated Tier List" → 計算ティアリスト. |
| Leaderboard | ランキング | Loanword. "World Rankings" → 世界ランキング, "UCS Leaderboard" → UCSランキング, "Bounty Leaderboard" → バウンティランキング, "Monthly Leaderboard" → 月間ランキング, "Score Rankings" → スコアランキング. |
| Communities | 地域ランキング | nabulator's choice — literally "regional rankings". Loses some social/community meaning. See Known issues. |
| Bounty / Bounties | バウンティ | Loanword. "Bounty Leaderboard" → バウンティランキング. |
| UCS | UCS | Untranslated acronym. "Add UCS" → UCS追加, "UCS Leaderboard" → UCSランキング. |
| Player | プレーヤー | Long-vowel ー retained (not プレイヤー). |
| Players | プレーヤー達 | nabulator added the 達 plural marker. Slightly unusual for game UI; see Known issues. |
| Player Count | プレーヤー数 | Drops 達 in compounds. |
| Player Level | プレーヤーレベル | |
| Tournament(s) | 大会 | "Tournament" → 大会, "Tournament Name" → 大会名, "Tournament Settings" → 大会設定. |
| Qualifiers | 資格 | "{0} Qualifiers Leaderboard" → {0}資格ランキング. |
| Rating | 評定 | "Max Rating" → 最高評定, "Rating Calculator" → 評定の計算. |
| Pumbility | (none) | Not yet translated; no current entries. Recommend leaving as `Pumbility` (loanword for proper-noun rating system). |
| Phoenix Score Calculator | Phoenixスコア計算 | |
| Score Loss | {0}スコア低下 | (in the X-form key "X Score Loss") |
| Song | 曲 | "Song Name" → 曲名, "Song Artist" → アーティスト, "Song Duration" → 曲時間, "Song Type" → 曲種類, "Song Image" → 曲画像. |
| Avatar | アバター | |
| Favorites | お気に入り / 気に入れ | "Add to Favorites" → お気に入りを追加 (canonical); "Favorites" bare → 気に入れ (nabulator, slightly off). Prefer お気に入り for new entries. |
| Weekly Charts | 週間譜面 | |

### Game-mechanic vocabulary (PIU Life Calculator etc.)

| English | ja-JP | Notes |
|---|---|---|
| Life / Health | 体力 | "Life Threshold" → 体力閾値, "Max Life" → 最大体力, "Starting Life" → 開始体力. |
| Lifebar | ライフバー | "Lifebar Description" → ライフバー説明. |
| Visible Life / Rainbow Life | 視覚体力 / 虹色ライフ | |
| Combo | コンボ | |
| Perfect / Great / Good / Bad / Miss | Perfect / Great / Good / Bad / Miss | All untranslated, kept English in mid-sentence. "Greats" → Great数, "Perfects" → Perfect数. |
| Multiplier | 倍率 | "Stage Break Modifier" → Stage失敗倍率. |
| Modifier | 倍率 | Same as multiplier. |
| Break / Broken | 失敗 / Break Off | "Stage Break" → not present, but "Broken" → "Break Off" (nabulator left English — see Known issues); "Bad Break" / "Miss Break" → Bad失敗 / Miss失敗. |

### Phrasing patterns to copy

- **Polite request**: `〜してください` for instructions to the user. Examples: `下のコピーバトン使ってください`, `ロック前にユーザー名を割り当ててください`, `ページを再読み込みして再試行してください`.
- **`注意：` prefix for warnings/disclaimers.** Used by nabulator and continued in the new batch: `注意：このスクリプトが壊しやすい...`, `注意：このデータはNX2とPrimeから抽出したもので...`. Place at sentence start, full-width colon.
- **Honorific `さん` for proper names in placeholders.** `Welcome to Score Tracker, {0}!` → `Score Trackerへようこそ、{0}さん！`. Same pattern for credit-style strings (`MR_WEQさん`, `daryenさん`, `KyleTTさん`, `FEFEMZさん`).
- **`〜について` for "about / regarding"**: `Life gain description` → 体力回復について, `Life loss description` → 体力減少について.
- **Show/hide as compound noun-suffix**: `Show Age` → 歳表示, `Show Difficulty` → 難度表示, `Hide Completed Charts` → 完了譜面隠す.

## Known issues / native review needed

These were carried over from nabulator's seed translations (PR #44) and intentionally **not** corrected during the machine-translation sweep, to keep structural and quality changes separate. A native-speaker pass should fold these in.

### Typos

- **`Mix` → `ベーション`** (line ≈ resx). Should be `バージョン` or — more accurately for this game — `ミックス` since "Mix" in PIU refers to game versions/iterations rather than a generic version number.
- **`Filters` → `フィアルた`**. Should be `フィルター`. The reordered characters are clearly a typo.
- **`Tools` → `ツルー`**. Should be `ツール`.
- **`To Do` → `やりこと`** (and downstream `やりことリスト`). Should be `やること` (and `やることリスト`). The typo propagates through "Add to ToDo", "Remove from To Do List", "Show Only ToDo Charts", "Toggle ToDo".
- **`Recorded On X` → `{0}にき記録した`**. Stray `き` — should be `{0}に記録した` or `{0}に記録されました` (polite-passive).
- **`一部のスコアダウンロードが失敗しまいました`** (Phoenix Import Saving Failures). `失敗しまいました` is non-standard — should be `失敗してしまいました` or just `失敗しました`.
- **`まえ` (Make Public Disclaimer 2)** — should be `前` (kanji) or `以前` for register consistency.

### Untranslated where translation is expected

- **`Country` → `Country`**. Should be `国`. nabulator left the dropdown label English; everywhere else the file uses Japanese for similar labels.
- **`Open` → `Open`**. Should be `オープン` or context-appropriate.
- **`Broken` → `Break Off`**. Left as English (and a different English phrase from the source). Should likely be `失敗` to match the rest of the break/fail vocabulary, or `Stage Break` to match the original PIU term.

### Questionable word choices

- **`CoOp Aggregation` → `CoOp集合`**. `集合` means "set/gathering", not "aggregation". Should be `集計` (which is what the new translation pass uses for similar concepts).
- **`Communities` → `地域ランキング`** ("regional rankings"). Loses the social-community connotation; the feature is about player groups, not just regions. Reasonable candidate: `コミュニティ`.
- **`Players` → `プレーヤー達`**. The `達` plural marker is grammatically valid but unusual in game UI, where bare `プレーヤー` is the norm. Compounds drop it correctly (`プレーヤー数`, `プレーヤーレベル`).
- **`Player Stats` → `進捗中の譜面`** ("in-progress charts"). nabulator interpreted "Stats" as the in-progress chart list this label is associated with on the page; not a literal translation. May or may not be the right call — depends on how the label reads in context.
- **`スコア(データあり)`** uses half-width parens, breaking the full-width convention. nabulator's choice; consistency-wise should be `スコア（データあり）`.
- **`シェアー`** (Chart List Share Description) — `シェアー` is non-standard katakana; standard would be `シェア`.
- **`捜索` vs `検索`** for "Search". Two forms in use; pick one (recommend `検索`).
- **`ユーザ` vs `ユーザー` / `プレーヤー`** — long-vowel mark inconsistency. nabulator uses `ユーザ`; the new batch uses `ユーザー`. Pick one and sweep.

### Machine-translation review priority

The 396 entries added in PR #90 were generated against nabulator's domain glossary. Dense paragraph strings are the highest review priority because subtle word-choice and sentence-flow issues hurt them most:

- **Life-bar mechanics paragraphs** (PIU Life Calculator page): `Bads at low health let you live...`, `Misses lose LESS health the further beneath 1000 health you are at...`, `Effectively, this means that your life gain is heavily affected by combo...`, `When at 12% or lower visual life, a miss gives less life loss than a bad...` — translations are technically correct but read mechanically.
- **Scoring-algorithm explainers** (ChartScoring page): `All players with scores within the target folder are assigned weights...`, `These averages are shifted away from the level in question by .5 of a standard deviation...`, `In this case, the chart is determined to be above by X utilizing standard deviations in the Y folder.` — same caveat, dense statistics prose.
- **Disclaimer/explainer paragraphs**: `Disclaimer: This data was data-mined in NX2 and Prime...`, `Disclaimer: This list is being refined...`.

For short labels (column headers, button text, single-noun keys), the machine pass is generally fine.

## Process for future batches

1. Pick a feature folder (Tournaments, Tier Lists, Progress, Admin, Tools, etc.) or a category from the Known issues list above.
2. Translate using this glossary. **If a new term needs a decision, add a row to "Established term mappings" before translating.**
3. For typo / questionable-word fixes, prefer one batch per category (e.g. "Fix yarikoto → yarukoto across the file") so the diff is reviewable.
4. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
5. PR titled like `Translate <Folder> to ja-JP` or `Fix ja-JP <typo / inconsistency>`.
