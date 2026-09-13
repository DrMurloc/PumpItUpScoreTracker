# check-new-charts step 2b: the channel each chart video sits in, from Andamiro's per-version
# playlists (docs/design/song-channels.md §6).
#
# The channel keeps one playlist per patch per channel - "[PIU PHOENIX 2] v1.01.0 - STEP CHART
# VIDEO (K-POP)" - so playlist membership says which channel a song's charts belong to. The
# playlists page lists the newest first, which is where the current patch's playlists are; one
# continuation is tried for older ones and tolerated when YouTube returns nothing for it.
#
# Writes channels.json in the state dir: { "<videoId>": "KPop", ... }, merged with what an
# earlier run wrote, so build-batch.ps1 can name each song's channel by its chart videos.
param(
    [string]$StateDir = "$env:USERPROFILE\.piu-score-tracker\check-new-charts",
    [string]$Mix = 'PHOENIX 2',   # the bracketed mix in the playlist title
    [string]$Version              # e.g. v1.01.0 - only that patch's playlists; default = every step-chart playlist of the mix on the page
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36'

$statePath = Join-Path $StateDir 'state.json'
if (-not (Test-Path $statePath)) { throw "no $statePath - bootstrap the state dir first (see SKILL.md)" }
$state = Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
$channelHandle = $state.channelHandle
if (-not $channelHandle) { $channelHandle = '@PUMPITUPOfficial' }

# the playlist title's parenthesis -> the enum name the blob's "channel" field takes
$tokens = @{ 'ORIGINAL' = 'Original'; 'K-POP' = 'KPop'; 'KPOP' = 'KPop'; 'WORLD MUSIC' = 'WorldMusic'; 'J-MUSIC' = 'JMusic'; 'XROSS' = 'Xross' }

# ---- the playlists page: id + title pairs ----
$r = Invoke-WebRequest -Uri "https://www.youtube.com/$channelHandle/playlists" -UserAgent $ua -TimeoutSec 30 -UseBasicParsing -Headers @{ 'Accept-Language' = 'en-US,en;q=0.9' }
$content = $r.Content
$apiKey = [regex]::Match($content, '"INNERTUBE_API_KEY":"([^"]+)"').Groups[1].Value
if (-not $apiKey) { throw 'no innertube key on the playlists page - YouTube markup changed?' }
$clientVersion = [regex]::Match($content, '"INNERTUBE_CONTEXT_CLIENT_VERSION":"([^"]+)"').Groups[1].Value
if (-not $clientVersion) { $clientVersion = '2.20250620.00.00' }

function Read-Playlists([string]$json, [hashtable]$into) {
    # a playlist lockup carries its id first and its title a little further on; whitespace-tolerant
    # because the browse API pretty-prints
    foreach ($m in [regex]::Matches($json, '"contentId":\s*"(PL[^"]+)",\s*"contentType":\s*"LOCKUP_CONTENT_TYPE_PLAYLIST"')) {
        $id = $m.Groups[1].Value
        if ($into.ContainsKey($id)) { continue }
        $tail = $json.Substring($m.Index, [Math]::Min(6000, $json.Length - $m.Index))
        $tm = [regex]::Match($tail, '"title":\s*\{\s*"content":\s*"([^"]+)"')
        if ($tm.Success) { $into[$id] = $tm.Groups[1].Value }
    }
}

$playlists = @{}
Read-Playlists $content $playlists
$tm = [regex]::Match($content, '"continuationCommand":\s*\{\s*"token":\s*"([^"]+)"')
if ($tm.Success) {
    try {
        $body = @{ context = @{ client = @{ clientName = 'WEB'; clientVersion = $clientVersion } }; continuation = $tm.Groups[1].Value } | ConvertTo-Json -Depth 5
        $resp = Invoke-WebRequest -Uri "https://www.youtube.com/youtubei/v1/browse?key=$apiKey" -Method Post -Body $body -ContentType 'application/json' -UserAgent $ua -TimeoutSec 30 -UseBasicParsing
        Read-Playlists $resp.Content $playlists
    } catch { Write-Output "NOTE: the playlists continuation failed ($($_.Exception.Message)) - the first page is what the newest patch needs" }
}

$wanted = @()
foreach ($id in $playlists.Keys) {
    $title = $playlists[$id]
    if ($title -notmatch 'STEP CHART') { continue }
    if ($title -notmatch [regex]::Escape("[PIU $Mix]")) { continue }
    if ($Version -and $title -notmatch [regex]::Escape($Version)) { continue }
    $pm = [regex]::Match($title, '\(([^)]+)\)\s*$')
    if (-not $pm.Success) { Write-Output "SKIP [$title] - no channel in parentheses"; continue }
    $key = $pm.Groups[1].Value.Trim().ToUpperInvariant()
    if (-not $tokens.ContainsKey($key)) { Write-Output "SKIP [$title] - '$($pm.Groups[1].Value)' is not a channel"; continue }
    $wanted += ,([pscustomobject]@{ Id = $id; Title = $title; Channel = $tokens[$key] })
}
Write-Output "PLAYLISTS: $($playlists.Count) on the page, $($wanted.Count) step-chart playlists for [PIU $Mix]$(if ($Version) { " $Version" })"
if ($wanted.Count -eq 0) { Write-Output 'nothing to fetch - is the patch on the channel yet?'; return }

# ---- each playlist's videos, through the browse call and its continuations ----
$channelsPath = Join-Path $StateDir 'channels.json'
$channels = @{}
if (Test-Path $channelsPath) {
    $old = Get-Content $channelsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($prop in $old.PSObject.Properties) { $channels[$prop.Name] = [string]$prop.Value }
}
$added = 0
foreach ($pl in $wanted) {
    $body = @{ context = @{ client = @{ clientName = 'WEB'; clientVersion = $clientVersion } }; browseId = "VL$($pl.Id)" } | ConvertTo-Json -Depth 5
    $resp = Invoke-WebRequest -Uri "https://www.youtube.com/youtubei/v1/browse?key=$apiKey" -Method Post -Body $body -ContentType 'application/json' -UserAgent $ua -TimeoutSec 30 -UseBasicParsing
    $json = $resp.Content
    $count = 0
    $rounds = 0
    while ($true) {
        foreach ($m in [regex]::Matches($json, '"contentId":\s*"([^"]{11})",\s*"contentType":\s*"LOCKUP_CONTENT_TYPE_VIDEO"')) {
            $vid = $m.Groups[1].Value
            if (-not $channels.ContainsKey($vid)) { $added++ }
            $channels[$vid] = $pl.Channel
            $count++
        }
        foreach ($m in [regex]::Matches($json, '"playlistVideoRenderer":\s*\{\s*"videoId":\s*"([^"]{11})"')) {
            $vid = $m.Groups[1].Value
            if (-not $channels.ContainsKey($vid)) { $added++ }
            $channels[$vid] = $pl.Channel
            $count++
        }
        $ct = [regex]::Match($json, '"continuationCommand":\s*\{\s*"token":\s*"([^"]+)"')
        $rounds++
        if (-not $ct.Success -or $rounds -ge 20) { break }
        $body = @{ context = @{ client = @{ clientName = 'WEB'; clientVersion = $clientVersion } }; continuation = $ct.Groups[1].Value } | ConvertTo-Json -Depth 5
        $resp = Invoke-WebRequest -Uri "https://www.youtube.com/youtubei/v1/browse?key=$apiKey" -Method Post -Body $body -ContentType 'application/json' -UserAgent $ua -TimeoutSec 30 -UseBasicParsing
        $json = $resp.Content
        Start-Sleep -Milliseconds 400
    }
    Write-Output ("  {0,-11} {1,4} videos  {2}" -f $pl.Channel, $count, $pl.Title)
    Start-Sleep -Milliseconds 600
}

$ordered = [ordered]@{}
foreach ($k in ($channels.Keys | Sort-Object)) { $ordered[$k] = $channels[$k] }
[IO.File]::WriteAllText($channelsPath, ($ordered | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
Write-Output "CHANNELS: $($channels.Count) videos mapped ($added new) -> $channelsPath"
