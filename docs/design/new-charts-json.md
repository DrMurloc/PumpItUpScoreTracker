# New-charts JSON blob (Bulk Add Charts)

The contract between the `/Admin/BulkAddCharts` tool and anything that produces new-song
JSON for it (today: hand-written by the owner; later: the automated "check for new Phoenix 2
charts" workflow below). Upload one `.json` file (a file picker, not a paste box — bulk blobs
overflow the SignalR message cap that Blazor textarea binds ride on) → validation preview →
Confirm → songs, charts, and the `ko-KR` culture-name rows are created. **Charts are always
created for Phoenix 2** — the tool has no mix picker; Phoenix-era content is considered complete.

Parsing/validation lives in `BulkChartJsonParser`
(`ScoreTracker.Domain/Services/BulkChartJsonParser.cs`, unit-tested in
`ScoreTracker.Tests/DomainTests/BulkChartJsonParserTests.cs`); catalog collision checks and
creation live in the page (`ScoreTracker/Pages/Admin/BulkAddCharts.razor`).

## Schema

Complete example (two songs, three charts):

```json
{
  "songs": [
    {
      "name": "District 1",
      "koreanName": "디스트릭트 1",
      "artist": "Doin",
      "type": "Arcade",
      "minBpm": 195,
      "maxBpm": 195,
      "durationSeconds": 105,
      "imageUrl": "https://example.com/images/district1.png",
      "charts": [
        {
          "type": "Single",
          "level": 22,
          "stepArtist": "EXC",
          "youtubeHash": "dQw4w9WgXcQ"
        },
        {
          "type": "Double",
          "level": 24,
          "stepArtist": "EXC",
          "youtubeHash": "aBcDeFgHiJk",
          "channelName": "SPHAM"
        }
      ]
    },
    {
      "name": "Baroque Virus Full Song",
      "koreanName": "바로크 바이러스 풀 송",
      "artist": "SHK",
      "type": "FullSong",
      "minBpm": 158,
      "maxBpm": 158,
      "durationSeconds": 223,
      "imageUrl": "https://example.com/images/baroque-virus-full.png",
      "charts": [
        {
          "type": "CoOp",
          "level": 2,
          "stepArtist": "SUNNY",
          "youtubeHash": "zYxWvUtSrQp"
        }
      ]
    }
  ]
}
```

### Song fields

| Field | Required | Notes |
|---|---|---|
| `name` | yes | English song title, stored verbatim (trimmed). Duplicate names **within one blob** are an error; a name already in the **Phoenix 2** catalog is skipped on Confirm; a name already in the **Phoenix** catalog only raises an informational notice (carried-over songs legitimately share names). |
| `koreanName` | yes | Feeds the `ko-KR` culture-name row (`SetSongCultureName`). **Korean-session score imports match on this** — omitting it breaks imports for players whose PIU account renders Korean titles, which is why it is required even for songs whose Korean title equals the English one. |
| `artist` | yes | Song artist (music, not steps). |
| `type` | yes | One of the `SongType` enum names, case-insensitive: `Arcade`, `ShortCut`, `FullSong`, `Remix`. |
| `minBpm` / `maxBpm` | yes (both) | Numbers, `> 0`, `maxBpm >= minBpm`. Fixed-tempo songs repeat the same value. |
| `durationSeconds` | yes | Integer seconds, `1`–`3600`. |
| `imageUrl` | yes | Absolute `http(s)` URL — give it the source Andamiro image (`song_img2/…`). On Confirm the image is **copied to the CDN** (`IFileUploadClient.CopyFromSource` → `piuimages.arroweclip.se/songs/<NameLettersAndDigits>.png`, matching the manual naming convention) and the song stores the CDN URL. A URL already on the CDN host is stored verbatim; a taken blob name (carried-over Phoenix song with the same name) gets a `-p2` suffix instead of overwriting the Phoenix art. If the copy fails, the source URL is stored, the results table says so, and the image can be rehosted later via the song-image update flow. |
| `charts` | yes | At least one chart. |

### Chart fields

| Field | Required | Notes |
|---|---|---|
| `type` | yes | `Single`, `Double`, or `CoOp` (case-insensitive). Performance charts are out of scope for this tool. |
| `level` | yes | Integer `1`–`29`. For `CoOp` charts this is the **player count** (e.g. `2`–`5`), matching how co-op charts are stored everywhere else. |
| `stepArtist` | yes | Step maker credit. |
| `youtubeHash` | yes | The YouTube video id only (letters, digits, `-`, `_`) — **not** a URL. Embedded as `https://www.youtube.com/embed/{hash}`, same as the one-at-a-time admin flow. |
| `channelName` | no | Defaults to `PUMP IT UP Official`. |

Property names are case-insensitive; unknown properties are ignored; trailing commas and
`//` comments are tolerated. Numbers may be quoted (`"minBpm": "195"` works), but emit real
numbers.

## Validation semantics

- **Errors block Confirm** (any error on any entry disables the button): malformed JSON,
  missing/blank required fields, unknown `type` values, out-of-range `level`/`durationSeconds`/BPM,
  non-absolute `imageUrl`, URL-shaped `youtubeHash`, zero charts, duplicate song name within the blob.
- **Warnings never block**: already-in-Phoenix-2 (entry is skipped on Confirm, everything else
  still runs), already-in-Phoenix (informational only — verify the carry-over is intentional).
- One bad song never hides the others: every entry gets its own preview card and error list.

## What Confirm does

Per song (already-in-Phoenix-2 entries skipped): copy `imageUrl` to the CDN (see the field
notes above — a failed copy falls back to the source URL and never blocks the song) →
`CreateSong` (which also persists the `ko-KR` culture-name row) →
`CreateChart(MixEnum.Phoenix2, …)` per chart. Each song runs in its own try/catch — a mid-run
failure is reported in the results table and the remaining songs still run. The chart cache is
cleared once at the end. Re-parsing after a partial run marks the songs that did get created
as "already exists", so re-Confirm only retries the failures.

## Collection workflow

Automated as the repo skill [.claude/skills/check-new-charts/](../../.claude/skills/check-new-charts/SKILL.md)
(scripts + durable state under `%USERPROFILE%\.piu-score-tracker\check-new-charts\`) — a
direct automation of the owner's manual process, so the blob stays the contract between the
tool and the workflow:

1. **YouTube watermark walk** — new charts are announced as per-chart videos on
   `youtube.com/@PUMPITUPOfficial` (batches of ~5 songs, BGA videos mixed in between). Given
   the newest already-processed video (the watermark), walk the uploads newest-first until it:
   skip BGA videos, group chart videos by song, and pull the chart list (type/level),
   `youtubeHash`, `stepArtist`, `artist`, and BPM from titles/descriptions, `durationSeconds`
   from video length.
2. **Official-site canonicalization** — YouTube titles carry small discrepancies, and name
   discrepancies break import matching, so the official site wins on names. Per song, from
   piugame.com: the canonical English `name`, the canonical Korean `koreanName`, and the
   `song_img2` `imageUrl`. A YouTube-vs-site name mismatch is flagged for review, never
   silently resolved.
3. **Emit the JSON** exactly in the schema above and upload it to `/Admin/BulkAddCharts` —
   the preview stays the human checkpoint before anything is written, and its
   already-in-catalog warnings are the dedup net when the watermark is stale.
