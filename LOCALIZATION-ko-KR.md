# ko-KR localization glossary

Working reference for translating `App.en-US.resx` into `App.ko-KR.resx`. Captures the conventions established by the existing 154 translated entries so future batches stay consistent.

For the localization mechanism itself (resx layout, `L["..."]` usage, key conventions), see [ARCHITECTURE.md](ARCHITECTURE.md#cross-cutting-concerns). For PIU domain terms in English, see [DOMAIN.md](DOMAIN.md). For the parallel ja-JP and pt-BR conventions, see [LOCALIZATION-ja-JP.md](LOCALIZATION-ja-JP.md) and [LOCALIZATION-pt-BR.md](LOCALIZATION-pt-BR.md).

## Style conventions

- **Formal-polite register (합쇼체, `-(스)ㅂ니다`).** Disclaimers, instructions, and full sentences end in `-습니다 / -ㅂ니다 / -됩니다`. Examples: `Score Tracker에는 암호가 저장되지 않습니다.`, `광고 차단기를 비활성화 하셔야 합니다.`, `업로드한 모든 채보가 성공적으로 업데이트 되었습니다!`. **Do not** drop into 해요체 (`-아요/-어요`) or 반말 (plain form). Polite requests use `해주세요` / `부탁드립니다` (`아이디만 입력해주세요!`, `매번 입력을 부탁드립니다`).
- **Sentence case is not a thing in Korean.** Ignore the en-US Title Case in keys; values just use natural Korean. English brand names embedded mid-sentence (`Phoenix`, `Score Tracker`, `XX`, `CSV`, `Spreadsheet`) keep their original casing.
- **Punctuation is half-width ASCII.** `()` not `（）`, `:` not `：`, `.` not `。`, `,` not `,`. (Korean uses ASCII punctuation natively, unlike Japanese.) Existing strings consistently use `?`, `!`, `.` half-width.
- **Numerals are half-width Arabic.** `1`, `2`, `10`, etc. — never full-width.
- **Spacing around English brand names mid-sentence.** Korean inserts a space between Hangul and embedded Latin tokens: `Phoenix 점수 계산기`, `Score Tracker 소개`, `Score Tracker에는 암호가...` (the particle `에는` attaches directly with no space, but the word boundary before the brand name has a space). Match the existing examples; particles attach to whichever side is grammatically attached.
- **Preserve positional placeholders verbatim.** `{0}`, `{1}`, `{2}` go into the value untouched, in whatever order Korean grammar wants. Example: key `Remaining Charts` (English value `{0} (SSS+) - {1} (AA) Charts Remaining`) → ko-KR value `{0} (SSS+) - {1} (AA) 남은 채보`. Example: key `Remaining Charts For You` → `{0}개의 채보가 남았습니다` (counter `개` follows the placeholder, then particle).
- **Skip prose with inline markup.** Per CLAUDE.md, `<MudText>` bodies with embedded `<MudLink>`/other elements stay hardcoded English. Don't extract them, don't translate them.
- **Honorific `님` for proper names in credits.** Same role as Japanese `さん`. Examples: `Mr_WEQ 님에게 감사드립니다`, `daryen님에게 감사드립니다`. Note: existing entries are inconsistent on the space before `님` (`Mr_WEQ 님` vs `daryen님`). Recommend **no space** before `님` — that's the more standard form. See Known issues.

## Established term mappings

These have at least one existing translation in `App.ko-KR.resx`. New translations of the same term must reuse the established form unless there's a documented reason to change it.

### App / generic UI

| English | ko-KR | Notes |
|---|---|---|
| Score Tracker (the app) | Score Tracker | Brand name, kept English. Used inside Korean sentences as-is. |
| About | 소개 | "About" → "Score Tracker 소개" (the bare About key includes the brand name in-value). |
| Account | 계정 | "Account" → 나의 계정 ("my account"). Standalone `Account` translates as 계정. |
| Account Creation? | 회원가입? | Korean web standard. |
| Actions | 실행 | |
| Add to Favorites | 좋아요 | nabulator-style — literal "like" (button label). Bare "Favorites" → 즐겨찾기. |
| Add to ToDo | 목표 목록에 추가 | "Remove from ToDo" → 목표 목록에서 빼기. |
| Average (as a difficulty bucket between Easy/Hard) | 중급 | "Intermediate-rank" reading. Distinct from a math/statistical "average" (which would be 평균). |
| Cancel | 취소 | |
| Close | 닫기 | |
| Communities | 커뮤니티 | Loanword. |
| Completed | 완료 | |
| Copy | 복사 | "Copy Script" → 스크립트 복사, "Copy to Clipboard" → 클립보드에 복사. Suffix pattern. |
| Difficulty Level | 난이도 | Single word; the `Level` is implicit. |
| Done | 완료 | Same as `Completed`. |
| Download | 다운로드 | Loanword. "Download Scores" → 스코어 다운로드, "Download Failures" → 다운로드 실패. |
| Easy / Hard | 쉬움 / 어려움 | Plain noun-form adjectives. |
| Easiest / Hardest Player | 하급 / 상급 | Rendered as "low-rank / high-rank" rather than literal -est. Same idea as ja-JP. |
| Ending Page | 마지막 페이지 | "Starting Page" → 홈페이지 (rendered as "homepage" — see Known issues). |
| Event | 이벤트 | "Event Links" → 이벤트 주소. Loanword. |
| Favorites | 즐겨찾기 | "Add to Favorites" is the outlier — uses 좋아요 instead. |
| Filters | (none yet) | Untranslated so far. |
| Full Privacy Policy | 개인정보 방침 | |
| Home | 메인으로 | Rendered as "to main" — sidebar nav label. |
| Language | 언어 | |
| Last Updated | 최근 업데이트 | |
| Login | 로그인 | |
| Logout | 로그아웃 | |
| Make Public / Private | 공개하기 / 비공개하기 | Verb form with 하기 suffix. Bare "Public" → 공개. |
| Medium | 중간 | |
| My Score | 내 점수 | Possessive `내` (informal-1st-person, but acceptable in this UI register). |
| Pending | 대기 중 | "In waiting." |
| Photos | 사진 | |
| Place (rank) | 등수 | "1st place" sense. "To Leaderboard" → 등수판으로 — note 등수 also forms 등수판 ("ranking board"). Distinct from `Leaderboard` → 리더보드. |
| Public / Private | 공개 / (none) | Bare "Public" present; bare "Private" not yet seen — when needed, use 비공개 (matches the verb pattern). |
| Restart | 재시작 | |
| Rules | 규칙 | |
| Save | 저장 | "Save Scores" → 점수 저장, "Saved Charts" → 저장된 채보. |
| Search | 검색 | "Official Leaderboard Search" → 공식 랭킹 검색. |
| Show | 표시 | "Show Score Distribution" → 점수 분포 표시. Suffix pattern. |
| Show Only X | X만 보기 | "Show Only ToDo Charts" → 목표 채보만 보기. Particle `만` ("only") + 보기 ("view"). |
| Submit | 제출 | "Submission Page" → 제출 페이지. |
| Title (in-game title award) | 칭호 | "Title Progress" → 칭호 진행상태, "Titles" → 칭호. |
| To Do | 목표 | Rendered as "goal." "Add to ToDo" → 목표 목록에 추가; "Show Only ToDo Charts" → 목표 채보만 보기. |
| Tools | 도구 | |
| Total Count | 총 | Rendered as bare 총 ("total"); the `Count` is implicit. |
| Tournaments | 대회 목록 | Note: includes `목록` ("list") suffix even though the English is bare plural. |
| Update | 업데이트 | "Last Updated" → 최근 업데이트. Loanword. |
| Upload | 업로드 / 등록 | Mixed. "Upload Image" → 사진 업로드 (loanword); "Upload Scores" → 점수 등록 (rendered as "register"); "Upload XX Scores" → XX 점수 등록. Lean on context. |
| Use Script | 스크립트 사용 | |
| Username | 아이디 | Korean web standard — `아이디` (loanword "ID") rather than literal 사용자 이름. |
| Very Easy / Very Hard | 아주 쉬움 / 아주 어려움 | |
| Video | 동영상 | "Open Video" → 동영상 보기, "Report Video" → 동영상 신고. |
| Vote Count | 투표 회수 | "{0} 투표 회수" — placeholder counts the votes. |
| Website | 웹사이트 | Loanword. |

### PIU domain

| English | ko-KR | Notes |
|---|---|---|
| Chart(s) | 채보 | Translated, not loanword. Plural "Charts" → 채보들 (with 들 marker). Used compositionally: `채보 목록`, `채보 종류`, `채보 갯수`, `저장된 채보`, `남은 채보`, `진행중인 채보`, `목표 채보`, `채보 무작위 생성기`. **Don't switch to 차트 mid-file** (one legacy entry uses 차트 — see Known issues). |
| Chart List Share | 차트 목록 공유 | One legacy `Chart List Share Description` uses 차트; everywhere else is 채보. See Known issues. |
| CoOp | 코옵 | **Translated** (loanword in Hangul), unlike ja-JP/pt-BR which keep "CoOp" English. "CoOp Aggregation" → 코옵 난이도 (rendered as "CoOp difficulty" — see Known issues). |
| Singles / Doubles | 싱글 / 더블 | Loanwords in Hangul, not 단식/복식. |
| Difficulty Level | 난이도 | (Repeated from generic UI for cross-reference.) |
| Letter Grade | 랭크 | Loanword (Hangul). "Next Letter" → 다음 랭크. |
| Mix | 시리즈 | Rendered as "series" — semantic translation, not loanword. (Compare ja-JP `バージョン` typo for `ベーション`, pt-BR `Versão`.) Some Korean PIU community usage prefers 믹스; this codebase chose 시리즈. |
| Phoenix / XX | Phoenix / XX | Game versions, untranslated proper nouns. "Phoenix Score Calculator" → Phoenix 점수 계산기; "Upload XX Scores" → XX 점수 등록; "Import Phoenix Scores" → Phoenix 점수 불러오기. |
| Plate | 플레이트 | Loanword (Hangul). |
| Pass (verb / noun for clearing a chart) | 클리어 / 성공 | Mixed. "Hide Completed Charts" → 클리어한 차트 숨기기 (uses Konglish 클리어한 = "cleared"); "Passed Count" → 성공; "Not Passed Count" → 실패; "Stage Pass" — not yet present. The pass/fail dyad is 성공/실패 for counts; 클리어 for the verb sense. |
| Score | 점수 | Translated (not loanword). "Score" → 점수, "Score State" → 기록 상태 (note: 기록 = "record"), "My Score" → 내 점수, "Score Loss" → "{0} 점수 감소", "Save Scores" → 점수 저장, "Official Scores" → 공식 점수, "Score Range" → score-range (in shoutout: 점수 등급의 범위). |
| Score (occasionally) | 스코어 | "Download Scores" → 스코어 다운로드 — one entry uses Konglish 스코어 instead of 점수. Inconsistency — see Known issues. |
| Phoenix Score Calculator | Phoenix 점수 계산기 | |
| Score Loss | 점수 감소 | "{0} Score Loss" → "{0} 점수 감소" (placeholder is the magnitude). |
| Note Count | (none yet) | Not yet present in ko-KR. |
| BPM | (none yet) | Not yet present in ko-KR. |
| Tier List | 서열표 | Translated — literally "rank chart/list." (Compare ja-JP `ティアリスト` loanword.) |
| Leaderboard | 리더보드 | Loanword (Hangul). |
| Rankings | 랭킹 | Loanword. "World Rankings" → 세계 랭킹, "Leaderboard Player Compare" → 플레이어 랭킹 비교. Distinct from `Leaderboard` → 리더보드. |
| Players | 유저 | Rendered as "user" — Korean PIU community / web UI convention. (Compare ja-JP `プレーヤー達`, pt-BR `Jogadores`.) |
| Tournament(s) | 대회 | "Tournaments" → 대회 목록. |
| Qualifiers | 예선과제 | Rendered as "preliminary task." "Qualifiers Leaderboard" → "{0} 예선과제 등수", "Qualifiers Submission" → "{0} 예선과제 제출". |
| Rating | 레이팅 | Loanword (Hangul). "Max Rating" → 최고 레이팅, "Rating Calculator" → 레이팅 계산기. |
| Pumbility | (none yet) | Not yet translated. Recommend leaving as `Pumbility` (proper-noun loanword) per the Phoenix/XX pattern. |
| Song | 음악 / 노래 | **Inconsistent.** Bare "Song" → 음악 ("music"); compounds use 노래 ("song"): `Song Name` → 노래 이름, `Song Image` → 노래 이미지, `Song Type` → 노래 종류, `Song Duration` → 노래 길이. See Known issues — should converge on one. |
| Song Artist | 작곡자 | Rendered as "composer." |
| Suggested Chart | 추천 채보 | |
| Title (in-game title award) | 칭호 | (Cross-reference: same as generic UI Title.) |
| Favorites | 즐겨찾기 | (Cross-reference.) |
| Progress | 진행상황 | "Progress Charts" → 진행중인 채보 (note: rendered as "in-progress charts," meaning the chart-list page, not generic-progress charts). |
| Popularity | 인기 | |

### Game-mechanic vocabulary

| English | ko-KR | Notes |
|---|---|---|
| Broken | 브레이크 오프 | Konglish "Break Off." Same treatment as ja-JP's untranslated "Break Off." Used in `Unpassed ToDos` → "{0}레벨 {1}채보에 '목표'로 표시된 것 중 아직 브레이크 오프가 있습니다." |
| Pass / Fail (counts) | 성공 / 실패 | "Passed Count" → 성공, "Not Passed Count" → 실패. |
| Not Graded | 랭크 없음 | "Not Graded Count" → 랭크 없음 ("no rank"). |

## Phrasing patterns to copy

- **Polite-formal register `-습니다 / -ㅂ니다`.** Used by every full-sentence string. Don't drop into 해요체 (`-아요/-어요`) or 반말.
- **Polite request: `~해주세요`.** "아래의 '스크립트 복사' 버튼을 사용하세요" / "아이디만 입력해주세요!" / "매번 입력을 부탁드립니다" — use `해주세요`/`부탁드립니다` for instructions and asks.
- **`참고:` prefix for warnings/disclaimers.** "참고: 이 과정은 상당히 불안정할 수 있으며..." / "참고: 점수 감소는 반올림으로 인해서 1-4점 차이가 날 수 있습니다." Place at sentence start, half-width colon, half-width space.
- **Honorific `님` for proper-name credits.** `Mr_WEQ 님에게 감사드립니다`, `daryen님에게 감사드립니다`. Equivalent role to ja-JP's `さん`.
- **Show / Hide as compound noun-suffix `~ 표시 / ~ 숨기기`.** `Show Score Distribution` → 점수 분포 표시; `Hide Completed Charts` → 클리어한 차트 숨기기.
- **Suffix-attached particles to placeholders.** `{0}개의 채보가 남았습니다` — the counter `개` and particle `의` follow the placeholder; same pattern for `{0}레벨 {1}채보에...`.
- **Make-X-state verbs use `~하기`.** `공개하기`, `비공개하기`. Verb-noun form, suitable for button labels.
- **Compound noun chains for column headers / labels.** `노래 길이`, `세계 랭킹`, `레이팅 계산기`, `채보 갯수` — space-separated, no possessive particle. Don't insert `의` unless the existing entry does.

## Known issues / native review needed

These were carried over from the existing translations and should be reviewed by a native speaker. Keep structural and quality changes separate diffs.

### Inconsistencies

- **`Song` → 음악 vs 노래.** Bare `Song` → 음악, but every `Song X` compound (`Song Name`, `Song Image`, `Song Type`, `Song Duration`) → 노래 X. Pick one: 노래 is the more colloquial fit; 음악 is more formal/musical. Recommend **converging on 노래** since the compounds outnumber the bare entry 4:1.
- **`Charts` → 채보 (consistent) except `Hide Completed Charts` → 클리어한 차트 숨기기.** That one entry uses Konglish 차트 instead of the file-wide 채보. Should be 클리어한 채보 숨기기.
- **`Score` → 점수 (consistent) except `Download Scores` → 스코어 다운로드.** That one entry uses Konglish 스코어. Should be 점수 다운로드 to match the rest of the file (`Save Scores` → 점수 저장, `Official Scores` → 공식 점수).
- **Honorific `님` spacing.** `Mr_WEQ 님에게` (with space) vs `daryen님에게` (no space). Korean style guide recommends **no space before `님`** — converge on `Mr_WEQ님에게`.
- **Loanword vs translation policy is not uniform.** `Charts` translated (채보) but `Plate` loanworded (플레이트); `Letter Grade` loanworded (랭크) but `Tier List` translated (서열표); `Leaderboard` loanworded (리더보드) but `World Rankings` partly-translated (세계 랭킹). The choices are individually defensible but the mix can feel arbitrary. No action recommended — document and continue, but note the pattern when adding new terms (default toward the choice the most-similar existing term made).

### Placeholder-order issues

- **`Log In With X` → "로 로그인 {0}".** Korean grammar wants `{0}로 로그인` (placeholder before the particle `로`). The current value is grammatically broken — placeholder comes after the particle that's supposed to attach to it. Should be `{0}로 로그인` or `{0} 계정으로 로그인`.
- **`Recorded On X` → "기록됨 {0}".** Korean wants `{0}에 기록됨` (placeholder + temporal particle `에` + verb). Current rendering puts the verb before the date, which reads awkwardly.
- **`Phoenix Import Saving Progress` → "{0}/{1} 업로드됨 {2} 남음 {3} 기록 실패".** Four-placeholder string with no connecting punctuation; reads as a terse status line. Acceptable but spartan; a native pass might add commas (`{0}/{1} 업로드됨, {2} 남음, {3} 기록 실패`).

### Questionable word choices

- **`Average` → 중급.** Defensible if `Average` means the difficulty bucket between Easy and Hard (which fits "intermediate"), but if any future English `Average` is used in a statistical sense (mean / average score), 중급 will be wrong. The math sense should be 평균. Disambiguate when the new key arrives.
- **`Mix` → 시리즈.** "Series" for "Mix" is interpretive. Korean PIU community usage tends toward 믹스 (loanword). Defensible either way; just don't introduce a third spelling.
- **`Players` → 유저.** Rendered as "user," not literal "player" (선수 / 플레이어). Korean web UI commonly uses 유저, so it reads fine — but it's a soft mismatch with the en-US `Players` label, especially in tournament contexts where 선수 ("competitor") might fit better. Recommend keeping 유저 unless tournament-specific copy needs distinguishing.
- **`Tournaments` → 대회 목록.** The `목록` ("list") suffix doesn't appear in the English source — the bare plural was extended. Acceptable on a navigation label that points to a list page, but if `Tournaments` ever appears as a column header or filter label, drop the 목록 to just 대회.
- **`CoOp Aggregation` → 코옵 난이도.** `난이도` means "difficulty," not "aggregation." The aggregation in question is the calculated CoOp difficulty score, so the rendering is contextually correct but lexically off — equivalent to ja-JP's `集合` / `集計` confusion. Better candidate: 코옵 집계.
- **`Username` → 아이디.** Standard Korean web UI but a loanword for a noun that has native Korean equivalents (사용자 이름, 사용자명, 닉네임). 아이디 reads natural for login forms; if `Username` ever appears as a generic profile-display label, 닉네임 might fit better.
- **`Starting Page` → 홈페이지.** Rendered as "homepage" rather than "starting page" — context-dependent (was used for the Phoenix-import script's start-from-page param). If `Starting Page` ever appears in a different context, this rendering will mislead.
- **`Score Range Shoutout` mixes 점수 (translated) and 스코어 (loanword).** "데이터 수집과 점수 등급의 범위" uses 점수, consistent with the file. No issue here — flagging only as a contrast to the `Download Scores` entry that uses 스코어 instead.

## Open decisions (terms upcoming batches will need)

Pulled from the ~440 untranslated keys. Each is a term that doesn't yet have an established ko-KR translation, and that future batches will hit. Decide once per term, then add the row to **Established term mappings** above and use it.

### PIU domain — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Pumbility | **Pumbility** (untranslated) | Proper noun for PIU's composite player rating. Matches Phoenix/XX policy. |
| UCS | **UCS** (untranslated) | Acronym (User-Created Step). Untranslated everywhere else. |
| Bounty / Bounties | 바운티 (Hangul loanword) | Suggested. Matches the Plate / Lifebar loanword treatment. |
| Stamina | 스태미나 (Hangul loanword) | Suggested. |
| BPM | BPM | Untranslated. "Min BPM" → "BPM 최소", "Max BPM" → "BPM 최대" (suggested — see Min/Max below). |
| Note Count | 노트 수 | Suggested. (`노트` is the established Korean PIU community term for a note/step.) |
| Step Artist | 스텝 아티스트 | Suggested loanword; alternative 채보 작곡자 if a translated form is preferred. |

### App / generic UI — high frequency

| English | Recommendation | Notes |
|---|---|---|
| Min / Max (bare) | 최소 / 최대 | Suggested. |
| Description | 설명 | Suggested. |
| Name | 이름 | Suggested (matches `노래 이름`). |
| Settings | 설정 | Suggested. |
| Image | 이미지 | Suggested loanword. |
| Edit | 수정 | Suggested. |
| Delete | 삭제 | Suggested. |
| Create | 생성 | Suggested. |
| Confirm | 확인 | Suggested. |
| Add | 추가 | Suggested (already used in compounds: `목표 목록에 추가`). |
| Welcome (greeting) | 환영합니다 | Suggested. |
| Tag(s) | 태그 | Suggested loanword. |
| Avatar | 아바타 | Suggested loanword. |

## Process for future batches

1. Pick a feature folder (Tournaments, Tier Lists, Progress, Admin, Tools, etc.) or a category from the Known issues list above.
2. List its English keys (`grep -oP '(?<=L\[")[^"]+' ScoreTracker/ScoreTracker/Pages/<Folder>/**/*.razor` or similar).
3. Cross-reference against `App.ko-KR.resx` to find which are missing.
4. Translate using this glossary. **If a new term needs a decision, add a row to "Established term mappings" before translating.**
5. For inconsistency fixes (e.g. converging 음악/노래, fixing the placeholder order in `Log In With`), do **one batch per category** so the diff is reviewable.
6. `dotnet build ScoreTracker/ScoreTracker.sln -c Release` to confirm resx well-formedness.
7. PR titled like `Translate <Folder> to ko-KR` or `Fix ko-KR <inconsistency>`.
