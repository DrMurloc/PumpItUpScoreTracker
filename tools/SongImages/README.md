# Song image maintenance

Two owner-run PowerShell scripts for the `piuimages.arroweclip.se` CDN (the `$web` container
of the `piuscores` storage account). Both are read-only until you drop `-WhatIf`, both are
idempotent, and neither needs `az`.

| Script | What it does |
|---|---|
| `refresh-song-images.ps1` | Re-downloads song jackets from piugame and overwrites the matching CDN blobs, content type included |
| `stamp-content-types.ps1` | Fixes the `application/octet-stream` backlog on blobs uploaded before PR #168, without re-uploading any bytes |

`PiuImages.psm1` is the shared helper (secrets, SAS, blob listing, content-type map).

## Why the previous script always errored

The predecessor (`Downloads\stamp-piuimages-content-types.ps1`) minted its SAS with
`az storage container generate-sas`. That command **succeeds**, but when no credential is
passed inline it first writes a multi-line `WARNING:` to **stderr** ("There are no
credentials provided in your command and environment, we will query for account key..."),
and Windows PowerShell 5.1 turns a native command's stderr into a `NativeCommandError`.
Under the `$ErrorActionPreference = 'Stop'` that script sets on line 21, that warning is
fatal — so it died on its SAS line on every run, before touching a single blob. Nothing was
wrong with Azure, the permissions, or the logic below it.

These scripts mint the SAS locally (HMAC-SHA256 over the account key already in the AppHost
user-secrets store), so `az` never runs and there is no stderr to trip over.

## Prerequisites

- The AppHost user-secrets store (id `2957ac5b-8cbb-49cf-be12-3b607ea9b818`) holding
  `AzureBlob:ConnectionSTring`, plus `PiuTest:Username` / `PiuTest:Password` for the crawl.
- `refresh-song-images.ps1` also needs the **local Aspire SQL container running**, because
  the prod-synced database is the authority on which blob path each song's art lives at —
  eleven non-arcade songs carry a GUID blob name rather than one derived from the title, and
  a carried-over Phoenix song can carry a `-p2` suffix. The script reads the container's
  host port from Docker rather than assuming one, since Aspire republishes it per run.

## Usage

```powershell
# census first, always - crawls, matches, downloads, compares, writes nothing
.\refresh-song-images.ps1 -WhatIf

# then the real run
.\refresh-song-images.ps1

# every song type, not just full songs / remixes / short cuts
.\refresh-song-images.ps1 -Types All

# reuse the crawl CSV already in the work dir instead of walking the board again
.\refresh-song-images.ps1 -SkipCrawl
```

```powershell
.\stamp-content-types.ps1 -WhatIf     # census
.\stamp-content-types.ps1             # stamp, then self-verify against a fresh listing
.\stamp-content-types.ps1 -Prefix songs/
```

**Both finish by printing a CDN purge command. Run it** — overwriting a blob does not
invalidate what the CDN already cached, so without a purge the old art (and the old
`application/octet-stream` header) keeps being served.

## What the refresh can and cannot reach

The jacket URLs come from piugame's monthly play-ranking board (`ajax/top_steps.php`, the
"song popularity" page), which is the one surface that exposes a song's *current* art. It
only covers songs still in **Phoenix 2**.

Phoenix-1-era songs cut from Phoenix 2 cannot be refreshed from piugame at all:
`phoenix.piugame.com` now 302s every request to Andamiro's `am-pass.net` SSO gateway, and
its board is no longer reachable by the direct login the tracker's own ACL uses. Those songs
are counted and listed as unmatched, and their blobs are left exactly as they are.

As of the 2026-09 board that splits the catalog 654 refreshable / 418 not — the 654 being
every Phoenix 2 song, with nothing in the mix left unmatched.

## How a song is matched, and why the mix is the answer

A jacket is tied to its song by piugame itself — the name and the image URL come out of the
same tile — so the only fuzzy step is tracker song ↔ piugame song.

**Titles are not unique across the catalog, but they are unique within a mix.** The game
cannot ship two songs called the same thing in one version, and this board is one mix's
entire catalog (654 tiles, 654 `Phoenix2` songs in the tracker — they agree exactly). So the
**mix is the disambiguator, not the name**: a row that is in `Phoenix2` is the row this board
is talking about, and a row that is not has no art here to claim.

That is what makes the twins safe. The catalog holds `Step` by KARA and `STEP` by SID-Sound
as two different songs with two different blobs, normalizing to one key; only SID-Sound's is
in Phoenix 2, so only it takes Phoenix 2's jacket and KARA's art is never touched. Same for
`Further` (Doin's is the P2 one) and the duplicated `Baroque Virus - FULL SONG -` row.
Matching on the *artist* instead gets these right only by luck — credit strings disagree
constantly (`feat.` dropped, a remixer added) — and wrong silently when they happen to agree.

Names are matched in two tiers. Tier 1 is the title as spelled, with every non-alphanumeric
character removed — the same transform the blob names use. Tier 2 exists because piugame
prints some titles with their Japanese or Korean original attached (`Kasou Shinja仮装信者`)
and sometimes carries a `feat.` credit the tracker omits (`CROSS RAY (feat. 月下Lia)` vs
`Cross Ray`); dropping the `feat.` clause and every non-ASCII character reconciles those four
without guessing, and a tier-2 hit must be the **only** candidate so it can never quietly
pick between two songs. Together they match 654 of 654.

A song that is in the mix but that the board never names is reported as `UNMATCHED` and left
alone — that means the two catalogs have drifted, not that the art is unchanged.

Both scripts also **read their work back** rather than trusting a 2xx — the uploader
re-fetches every blob it wrote from the origin (not the CDN, so a stale edge cache cannot
fake a pass) and checks both the bytes and the served content type; the stamper re-lists and
re-checks. And a downloaded file has to prove it is really an image of the right format
before it can overwrite anything, because a fetch that answered with an error page or a
truncated body still lands on disk under the right name.

## Known catalog quirks this surfaced

Not caused by the scripts, and not fixed by them — worth knowing:

- `Further` by Doin and `Further` by DJ Bouche feat. EZGi are two different songs **sharing
  one blob** (`songs/Further.png`), so one of them already shows the other's art.
- `Adios` (Eun Ji Won / Everglow) and `PICK ME` each exist as two rows with different blobs.
- `PRiMA MATERiA - SHORT CUT -` is stored with `Type = Arcade` rather than `ShortCut`.

## Notes

- Blob paths never change, so a refresh needs **no database update** — the art the site
  already points at is replaced in place.
- Only jackets whose bytes actually differ are uploaded, so a re-run after a partial failure
  is cheap and a no-op run purges nothing. `-Force` overrides that.
- 161 blobs in the container have a literal `%2f` in their name (`avatars%2f<hash>.png`) —
  orphans from an older upload that escaped the whole path as one segment instead of per
  segment. No page serves them. The stamp script reports them and leaves them alone;
  deleting them is a separate call.
