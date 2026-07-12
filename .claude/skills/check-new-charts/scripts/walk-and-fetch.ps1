# check-new-charts step 2: walk @PUMPITUPOfficial uploads newest -> watermark, fetch watch pages.
#
# The channel grid's id<->title pairing is OFF-BY-ONE in the innertube payload - never trust it.
# Watch pages (ytInitialPlayerResponse.videoDetails) are authoritative for title/description/length
# and are cached one JSON per video id, so re-runs and multi-run song completion are cheap.
#
# Advances state.json's watermark to the newest id seen ONLY when the watermark was reached and
# every watch page fetched - a partial window never loses videos because the cache accumulates.
param(
    [string]$StateDir = "$env:USERPROFILE\.piu-score-tracker\check-new-charts",
    [string]$Watermark  # override; default = state.json's watermark
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36'

$statePath = Join-Path $StateDir 'state.json'
if (-not (Test-Path $statePath)) { throw "no $statePath - bootstrap the state dir first (see SKILL.md)" }
$state = Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $Watermark) { $Watermark = $state.watermark }
if (-not $Watermark) { throw 'no watermark in state.json and none passed via -Watermark' }
$channelHandle = $state.channelHandle
if (-not $channelHandle) { $channelHandle = '@PUMPITUPOfficial' }
$videoDir = Join-Path $StateDir 'videos'
New-Item -ItemType Directory -Force $videoDir | Out-Null

# ---- Phase A: walk the uploads tab newest -> watermark, collecting video ids ----
$r = Invoke-WebRequest -Uri "https://www.youtube.com/$channelHandle/videos" -UserAgent $ua -TimeoutSec 30 -UseBasicParsing -Headers @{ 'Accept-Language' = 'en-US,en;q=0.9' }
$content = $r.Content
$apiKey = [regex]::Match($content, '"INNERTUBE_API_KEY":"([^"]+)"').Groups[1].Value
if (-not $apiKey) { throw 'no innertube key on the channel page - YouTube markup changed?' }
# the continuation POST rejects stale client versions - always take the page's own
$clientVersion = [regex]::Match($content, '"INNERTUBE_CONTEXT_CLIENT_VERSION":"([^"]+)"').Groups[1].Value
if (-not $clientVersion) { $clientVersion = '2.20250620.00.00' }

$ids = New-Object System.Collections.ArrayList
$seen = @{}
$found = $false
$page = 0
while ($true) {
    $page++
    # 2025 lockupViewModel shape; whitespace-tolerant because the browse API pretty-prints
    foreach ($m in [regex]::Matches($content, '"contentId":\s*"([^"]{11})",\s*"contentType":\s*"LOCKUP_CONTENT_TYPE_VIDEO"')) {
        $id = $m.Groups[1].Value
        if ($seen.ContainsKey($id)) { continue }
        $seen[$id] = $true
        if ($id -eq $Watermark) { $found = $true; break }
        [void]$ids.Add($id)
    }
    if ($found) { break }
    $tm = [regex]::Match($content, '"continuationCommand":\s*\{\s*"token":\s*"([^"]+)"')
    if (-not $tm.Success -or $page -ge 20) { break }
    $body = @{ context = @{ client = @{ clientName = 'WEB'; clientVersion = $clientVersion } }; continuation = $tm.Groups[1].Value } | ConvertTo-Json -Depth 5
    $resp = Invoke-WebRequest -Uri "https://www.youtube.com/youtubei/v1/browse?key=$apiKey" -Method Post -Body $body -ContentType 'application/json' -UserAgent $ua -TimeoutSec 30 -UseBasicParsing
    $content = $resp.Content
    Start-Sleep -Milliseconds 400
}

[IO.File]::WriteAllLines((Join-Path $StateDir 'ids-new.txt'), [string[]]$ids, (New-Object System.Text.UTF8Encoding($false)))
Write-Output "WALK: $($ids.Count) new ids (newest -> watermark) over $page page(s); watermark reached: $found"
if (-not $found) { Write-Output 'WARNING: watermark not reached within the page cap - window may be incomplete; watermark will NOT advance' }
if ($ids.Count -eq 0) { Write-Output 'NO NEW VIDEOS since the watermark - nothing to do'; return }

# ---- Phase B: fetch each watch page (cached; authoritative title/desc/length) ----
$done = 0
$failed = 0
foreach ($id in $ids) {
    $file = Join-Path $videoDir "$id.json"
    if (Test-Path $file) { $done++; continue }
    try {
        $w = Invoke-WebRequest -Uri "https://www.youtube.com/watch?v=$id" -UserAgent $ua -TimeoutSec 30 -UseBasicParsing -Headers @{ 'Accept-Language' = 'en-US,en;q=0.9' }
        $pm = [regex]::Match($w.Content, 'ytInitialPlayerResponse\s*=\s*(\{.+?\})\s*;\s*(?:var\s|</script>)', 'Singleline')
        if (-not $pm.Success) { throw 'no player response' }
        $d = ($pm.Groups[1].Value | ConvertFrom-Json).videoDetails
        $out = [ordered]@{
            videoId       = $d.videoId
            title         = $d.title
            channelId     = $d.channelId
            lengthSeconds = [int]$d.lengthSeconds
            description   = $d.shortDescription
        } | ConvertTo-Json -Depth 4
        [IO.File]::WriteAllText($file, $out, (New-Object System.Text.UTF8Encoding($false)))
        $done++
    }
    catch {
        $failed++
        Write-Output "FETCHFAIL $id : $($_.Exception.Message)"
    }
    Start-Sleep -Milliseconds 350
}
Write-Output "FETCH: $done ok, $failed failed, of $($ids.Count)"

# ---- advance the watermark (two-phase: only on a complete, fully-cached window) ----
if ($found -and $failed -eq 0) {
    $state.watermark = $ids[0]
    $state | Add-Member -NotePropertyName updatedUtc -NotePropertyValue ([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')) -Force
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    Write-Output "WATERMARK advanced to $($ids[0])"
}
else {
    Write-Output 'WATERMARK unchanged (incomplete window or fetch failures) - fix and rerun; fetched pages are cached'
}

# ---- window summary (titles of just this window, for the report/agent) ----
foreach ($id in $ids) {
    $file = Join-Path $videoDir "$id.json"
    if (Test-Path $file) {
        $v = Get-Content $file -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Output ("NEW  {0}  {1}s  {2}" -f $v.videoId, $v.lengthSeconds, $v.title)
    }
}
