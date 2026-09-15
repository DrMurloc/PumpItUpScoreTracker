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

As of the 2026-09 board that split the non-arcade catalog 116 refreshable / 118 not.

## Notes

- Blob paths never change, so a refresh needs **no database update** — the art the site
  already points at is replaced in place.
- Only jackets whose bytes actually differ are uploaded, so a re-run after a partial failure
  is cheap and a no-op run purges nothing. `-Force` overrides that.
- 161 blobs in the container have a literal `%2f` in their name (`avatars%2f<hash>.png`) —
  orphans from an older upload that escaped the whole path as one segment instead of per
  segment. No page serves them. The stamp script reports them and leaves them alone;
  deleting them is a separate call.
