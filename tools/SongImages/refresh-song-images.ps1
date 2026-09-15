<#
.SYNOPSIS
    Re-downloads song jackets from piugame and re-uploads them to the piuimages CDN with
    the correct content type.

.DESCRIPTION
    Walks piugame's monthly play-ranking board (ajax/top_steps.php, the "song popularity"
    page), which is the one surface that lists a song's CURRENT jacket URL, then matches
    each song to its tracker row and overwrites that song's existing blob in place. Because
    the blob path never changes, no database update is needed - the art the site already
    points at is simply replaced.

    Only songs whose fresh bytes actually differ are uploaded, so a re-run after a partial
    failure is cheap and a no-op run purges nothing.

    Scope note: the board only covers songs still in Phoenix 2. Phoenix-1-era songs cut from
    P2 cannot be refreshed from piugame at all - phoenix.piugame.com now redirects to
    Andamiro's am-pass SSO gateway and its board is no longer reachable. Those songs are
    reported as unmatched and left exactly as they are.

.EXAMPLE
    .\refresh-song-images.ps1 -WhatIf
    Crawls, matches, downloads, compares - and writes nothing to Azure.

.EXAMPLE
    .\refresh-song-images.ps1
    The same, then uploads every jacket whose bytes changed.

.EXAMPLE
    .\refresh-song-images.ps1 -Types All
    Every song type, not just the full songs / remixes / short cuts.
#>
[CmdletBinding()]
param(
    [string[]]$Types = @('FullSong', 'Remix', 'ShortCut'),
    [string]$Date,
    [string]$WorkDir = (Join-Path $env:USERPROFILE 'Downloads\song-images'),
    [switch]$WhatIf,
    [switch]$SkipCrawl,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Import-Module (Join-Path $PSScriptRoot 'PiuImages.psm1') -Force

$userAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0'
$piuBase = 'https://piugame.com'
if (-not $Date) { $Date = (Get-Date).ToUniversalTime().AddDays(-1).ToString('yyyyMM') }
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$imageDir = Join-Path $WorkDir 'jackets'
New-Item -ItemType Directory -Force $imageDir | Out-Null
$crawlCsv = Join-Path $WorkDir "popularity-$Date.csv"

# Blob names are the song name with every non-alphanumeric character removed - the owner's
# manual convention, which BulkAddCharts also follows. Matching normalizes the same way so
# "Bad Apple!! feat. Nomico - FULL SONG -" lines up with BadApplefeatNomicoFULLSONG.png.
function Get-NameKey {
    param([string]$Name)
    if (-not $Name) { return '' }
    $flat = $Name.Replace([char]0x2019, "'").Replace([char]0x2018, "'")
    return (($flat.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) }) -join '').ToLowerInvariant()
}

function Get-Md5 {
    param([byte[]]$Bytes)
    $md5 = [Security.Cryptography.MD5]::Create()
    try { return [Convert]::ToBase64String($md5.ComputeHash($Bytes)) } finally { $md5.Dispose() }
}

# ---------------------------------------------------------------- 1. crawl the board
if ($SkipCrawl -and (Test-Path $crawlCsv)) {
    $crawled = @(Import-Csv $crawlCsv)
    Write-Host "reusing $($crawled.Count) crawled songs from $crawlCsv"
}
else {
    $user = Get-PiuSecret 'PiuTest:Username'
    $pass = Get-PiuSecret 'PiuTest:Password'
    if (-not $user) { throw 'no PiuTest credentials in the user-secrets store' }

    $session = $null
    try { Invoke-WebRequest -Uri "$piuBase/" -SessionVariable session -UserAgent $userAgent -TimeoutSec 20 -UseBasicParsing | Out-Null }
    catch { if ($null -eq $session) { $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession } }
    Invoke-WebRequest -Uri "$piuBase/bbs/login_check.php" -Method Post -UseBasicParsing `
        -Body @{ url = '/'; mb_id = $user; mb_password = $pass } -WebSession $session -UserAgent $userAgent -TimeoutSec 25 | Out-Null
    # Song names follow the SESSION language, not Accept-Language, and the tracker's names
    # are canonical English.
    try {
        Invoke-WebRequest -Uri "$piuBase/ajax/language_update.php" -Method Post -UseBasicParsing `
            -Body @{ lang = 'en' } -WebSession $session -UserAgent $userAgent -TimeoutSec 20 | Out-Null
    }
    catch { Write-Host 'WARNING: language_update failed - names may come back Korean; verify before uploading' }

    Write-Host "crawling the $Date play-ranking board..."
    $tiles = @()
    $page = 0
    while ($true) {
        $html = ''
        foreach ($attempt in 1..3) {
            try {
                $resp = Invoke-WebRequest -Uri "$piuBase/ajax/top_steps.php" -Method Post -UseBasicParsing `
                    -Body @{ page = "$page"; date = $Date; mode = 'full' } -WebSession $session -UserAgent $userAgent -TimeoutSec 30
                $html = [Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray())
            }
            catch { $html = '' }
            if ($html) { break }
            Start-Sleep -Milliseconds (500 * $attempt)
        }
        $chunks = @($html -split '<li' | Select-Object -Skip 1)
        if ($chunks.Count -eq 0) { break }
        foreach ($chunk in $chunks) {
            $img = [regex]::Match($chunk, "background-image:url\('([^']*song_img2?/[^']+)'\)")
            $name = [regex]::Match($chunk, '<p class="t1">\s*([^<]*)</p>')
            $artist = [regex]::Match($chunk, '<p class="t2">\s*([^<]*)</p>')
            if (-not ($img.Success -and $name.Success)) { continue }
            # The artist rides along because two different songs can share a title - it is
            # the only field that tells KARA's "Step" from SID-Sound's "STEP".
            $tiles += [pscustomobject]@{
                Name     = [Net.WebUtility]::HtmlDecode($name.Groups[1].Value).Trim()
                Artist   = if ($artist.Success) { [Net.WebUtility]::HtmlDecode($artist.Groups[1].Value).Trim() } else { '' }
                ImageUrl = $img.Groups[1].Value
            }
        }
        if ($chunks.Count -lt 50) { break }
        $page += 50
        if ($page -gt 20000) { Write-Host 'safety stop at page 20000'; break }
        Start-Sleep -Milliseconds 300
    }
    $crawled = @($tiles | Group-Object Name | ForEach-Object {
            $first = $_.Group | Select-Object -First 1
            [pscustomobject]@{ Name = $_.Name; Artist = $first.Artist; ImageUrl = $first.ImageUrl }
        })
    if ($crawled.Count -eq 0) { throw "the board returned no songs for $Date - check the login and the date" }
    $crawled | Sort-Object Name | Export-Csv $crawlCsv -NoTypeInformation -Encoding UTF8
    Write-Host "  $($tiles.Count) chart tiles -> $($crawled.Count) distinct songs -> $crawlCsv"
}

# ------------------------------------------------- 2. the tracker's songs and their blobs
# The local Aspire database is prod-synced and is the authority on which blob path each
# song's art lives at - 11 non-arcade songs carry a GUID blob name rather than a derived
# one, and a carried-over Phoenix song can carry a -p2 suffix, so the path cannot simply be
# recomputed from the name. Aspire republishes the SQL container on a fresh random host
# port every run, so the port is read from Docker rather than assumed.
function Get-TrackerSongs {
    $ports = docker ps --filter 'ancestor=mcr.microsoft.com/mssql/server:2025-latest' --format '{{.Ports}}'
    $line = @($ports) | Select-Object -First 1
    if (-not $line) { throw 'no local SQL container is running - start the Aspire AppHost first' }
    $matched = [regex]::Match($line, ':(\d+)->1433')
    if (-not $matched.Success) { throw "could not read the SQL port from: $line" }
    $connection = New-Object System.Data.SqlClient.SqlConnection(
        "Server=127.0.0.1,$($matched.Groups[1].Value);Database=ScoreTracker;User Id=sa;Password=LocalDev_Passw0rd!;TrustServerCertificate=True;Connect Timeout=30")
    $connection.Open()
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT Name, Type, Artist, ImagePath FROM scores.Song'
        $reader = $command.ExecuteReader()
        $rows = @()
        while ($reader.Read()) {
            $rows += [pscustomobject]@{
                Name = $reader['Name']; Type = $reader['Type']
                Artist = $reader['Artist']; ImagePath = $reader['ImagePath']
            }
        }
        $reader.Close()
        return $rows
    }
    finally { $connection.Close() }
}

$allSongs = @(Get-TrackerSongs)
Write-Host "tracker catalog: $($allSongs.Count) songs"
$songs = $allSongs
if ($Types -notcontains 'All') { $songs = @($allSongs | Where-Object { $Types -contains $_.Type }) }
Write-Host "in scope ($($Types -join ', ')): $($songs.Count) songs"

$byKey = @{}
foreach ($c in $crawled) {
    $key = Get-NameKey $c.Name
    if ($key -and -not $byKey.ContainsKey($key)) { $byKey[$key] = $c }
}

# Titles are not unique. The catalog holds "Step" by KARA and "STEP" by SID-Sound as two
# different songs with two different blobs, and they normalize to the same key - so a
# name-only match would write SID-Sound's jacket over KARA's art and nothing would ever say
# so. Any key that points at more than one blob ACROSS THE WHOLE CATALOG (computed before
# the type filter, since the twins need not share a type) is ambiguous, and an ambiguous
# match is only accepted when the artist agrees too.
$blobsPerKey = @{}
foreach ($song in $allSongs) {
    $key = Get-NameKey $song.Name
    if (-not $key) { continue }
    if (-not $blobsPerKey.ContainsKey($key)) { $blobsPerKey[$key] = New-Object 'System.Collections.Generic.HashSet[string]' }
    [void]$blobsPerKey[$key].Add([string]$song.ImagePath)
}

function Get-ArtistKey {
    param([string]$Artist)
    if (-not $Artist) { return '' }
    return (($Artist.ToCharArray() | Where-Object { [char]::IsLetterOrDigit($_) }) -join '').ToLowerInvariant()
}

$plan = @()
$unmatched = @()
$ambiguous = @()
foreach ($song in $songs) {
    # Only CDN-hosted art can be replaced in place; a song still pointing at a foreign host
    # has no blob of ours to overwrite.
    if ($song.ImagePath -notlike 'https://piuimages.arroweclip.se/*') { continue }
    $key = Get-NameKey $song.Name
    if (-not $byKey.ContainsKey($key)) { $unmatched += $song; continue }
    $candidate = $byKey[$key]
    if ($blobsPerKey[$key].Count -gt 1 -and (Get-ArtistKey $song.Artist) -ne (Get-ArtistKey $candidate.Artist)) {
        $ambiguous += [pscustomobject]@{ Song = $song; Candidate = $candidate }
        continue
    }
    $blobPath = $song.ImagePath -replace '^https://piuimages\.arroweclip\.se/', ''
    $plan += [pscustomobject]@{
        Name     = $song.Name
        Type     = $song.Type
        BlobPath = $blobPath
        SourceUrl = $candidate.ImageUrl
        LocalFile = Join-Path $imageDir (($blobPath -split '/')[-1])
    }
}

# Two tracker rows sharing one blob (a genuine duplicate song row) would otherwise be
# downloaded and uploaded twice, and the second upload could differ from the first.
$plan = @($plan | Group-Object BlobPath | ForEach-Object { $_.Group | Select-Object -First 1 })

Write-Host ''
Write-Host "matched to piugame art: $($plan.Count)"
$plan | Group-Object Type | Sort-Object Name | ForEach-Object { Write-Host ("  {0,4}  {1}" -f $_.Count, $_.Name) }
Write-Host "no longer on piugame (cut from Phoenix 2, left untouched): $($unmatched.Count)"
$unmatched | Group-Object Type | Sort-Object Name | ForEach-Object { Write-Host ("  {0,4}  {1}" -f $_.Count, $_.Name) }
if ($ambiguous.Count -gt 0) {
    Write-Host ''
    Write-Host "SKIPPED $($ambiguous.Count) - another song shares this title and the artist does not match:"
    foreach ($a in $ambiguous) {
        Write-Host ("    {0} by {1}  ->  piugame has '{2}' by {3}" -f `
                $a.Song.Name, $a.Song.Artist, $a.Candidate.Name, $a.Candidate.Artist)
        Write-Host ("      left alone: {0}" -f ($a.Song.ImagePath -replace '^https://piuimages\.arroweclip\.se/', ''))
    }
}
if ($plan.Count -eq 0) { Write-Host ''; Write-Host 'nothing to refresh.'; return }

# ------------------------------------------------------------- 3. download the fresh art
Write-Host ''
Write-Host 'downloading fresh jackets...'
$downloaded = 0
$downloadFailed = @()
foreach ($item in $plan) {
    try {
        Invoke-WebRequest -Uri $item.SourceUrl -OutFile $item.LocalFile -UseBasicParsing -UserAgent $userAgent -TimeoutSec 30
        $downloaded++
    }
    catch { $downloadFailed += "$($item.Name)  ->  $($_.Exception.Message)" }
    if ($downloaded -gt 0 -and ($downloaded % 50) -eq 0) { Write-Host "  $downloaded / $($plan.Count)" }
}
Write-Host "  downloaded $downloaded of $($plan.Count) to $imageDir"
$downloadFailed | Select-Object -First 10 | ForEach-Object { Write-Host "  DOWNLOAD FAILED: $_" }

# A download that answered with an error page, a login redirect or a truncated body still
# lands on disk under the right name. The file's own magic bytes have to agree with the
# extension of the blob it is about to replace, or it is dropped rather than uploaded -
# overwriting real art with a broken image is the one failure here that is not self-healing.
$rejected = @()
$plan = @($plan | Where-Object {
        if (-not (Test-Path $_.LocalFile)) { return $false }
        $bytes = [IO.File]::ReadAllBytes($_.LocalFile)
        $format = Get-ImageFormat -Bytes $bytes
        $wanted = if ($_.BlobPath -match '\.([A-Za-z0-9]+)$') { $Matches[1].ToLowerInvariant() } else { '' }
        if ($wanted -eq 'jpeg') { $wanted = 'jpg' }
        if (-not $format) { $rejected += "$($_.Name)  ->  not an image ($($bytes.Length) bytes)"; return $false }
        if ($format -ne $wanted) { $rejected += "$($_.Name)  ->  downloaded $format but the blob is .$wanted"; return $false }
        return $true
    })
if ($rejected.Count -gt 0) {
    Write-Host "  REJECTED $($rejected.Count) download(s) that are not valid art for their blob:"
    $rejected | ForEach-Object { Write-Host "    $_" }
}
Write-Host "  $($plan.Count) verified image(s) ready"

# --------------------------------------------- 4. compare against what the CDN serves now
$account = Get-BlobAccount
$permissions = if ($WhatIf) { 'rl' } else { 'rcwl' }
$sas = New-ContainerSas -Account $account -Permissions $permissions -Hours 6
$base = Get-ContainerBaseUrl -Account $account

Write-Host ''
Write-Host 'comparing against the live CDN blobs...'
$changed = @()
$same = 0
$missing = 0
foreach ($item in $plan) {
    $fresh = Get-Md5 ([IO.File]::ReadAllBytes($item.LocalFile))
    $url = "$base/$(ConvertTo-BlobPath $item.BlobPath)?$sas"
    $current = $null
    try {
        $existing = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30
        $current = Get-Md5 $existing.RawContentStream.ToArray()
    }
    catch { $missing++ }
    if ($null -ne $current -and $current -eq $fresh -and -not $Force) { $same++; continue }
    $changed += $item
}
Write-Host "  unchanged: $same    different: $($changed.Count)    not yet on the CDN: $missing"
if ($Force) { Write-Host '  -Force: uploading every matched jacket regardless' }

# ------------------------------------------------------------------------- 5. upload
if ($changed.Count -eq 0) { Write-Host ''; Write-Host 'every jacket on the CDN already matches piugame - nothing to upload.'; return }
$changed | Select-Object -First 20 | ForEach-Object { Write-Host "    $($_.BlobPath)   <-  $($_.Name)" }
if ($changed.Count -gt 20) { Write-Host "    ... and $($changed.Count - 20) more" }

if ($WhatIf) { Write-Host ''; Write-Host '-WhatIf: no changes made.'; return }

Write-Host ''
$uploaded = 0
$uploadFailed = @()
foreach ($item in $changed) {
    $contentType = Get-ContentTypeForPath $item.BlobPath
    if (-not $contentType) { $uploadFailed += "$($item.BlobPath)  ->  unmapped extension"; continue }
    $headers = @{
        'x-ms-version'           = '2022-11-02'
        'x-ms-blob-type'         = 'BlockBlob'
        'x-ms-blob-content-type' = $contentType
    }
    try {
        Invoke-WebRequest -Method Put -Uri "$base/$(ConvertTo-BlobPath $item.BlobPath)?$sas" -Headers $headers `
            -InFile $item.LocalFile -ContentType $contentType -UseBasicParsing | Out-Null
        $uploaded++
    }
    catch { $uploadFailed += "$($item.BlobPath)  ->  $($_.Exception.Message)" }
    if ($uploaded -gt 0 -and ($uploaded % 25) -eq 0) { Write-Host "  $uploaded / $($changed.Count)" }
}

Write-Host ''
Write-Host "uploaded $uploaded of $($changed.Count). Failed: $($uploadFailed.Count)"
$uploadFailed | Select-Object -First 10 | ForEach-Object { Write-Host "  FAILED: $_" }

# Read every blob back and confirm it now IS the file that was sent, header included. A 201
# says the service accepted the request, not that the blob a player will fetch is the right
# art - and this is the run that overwrote the only copy, so "probably fine" is not good
# enough. Reads go to the origin, not the CDN, so a stale edge cache cannot fake a pass.
Write-Host ''
Write-Host 'Verifying every upload by reading it back from the origin...'
$verified = 0
$bad = @()
foreach ($item in $changed) {
    $expected = Get-Md5 ([IO.File]::ReadAllBytes($item.LocalFile))
    try {
        $check = Invoke-WebRequest -Uri "$base/$(ConvertTo-BlobPath $item.BlobPath)?$sas" -UseBasicParsing -TimeoutSec 30
        $actualMd5 = Get-Md5 $check.RawContentStream.ToArray()
        $actualType = $check.Headers['Content-Type']
        $wantType = Get-ContentTypeForPath $item.BlobPath
        if ($actualMd5 -ne $expected) { $bad += "$($item.BlobPath)  ->  bytes differ from what was sent" }
        elseif ($actualType -ne $wantType) { $bad += "$($item.BlobPath)  ->  serving $actualType, expected $wantType" }
        else { $verified++ }
    }
    catch { $bad += "$($item.BlobPath)  ->  could not read back: $($_.Exception.Message)" }
}
Write-Host "  verified $verified of $($changed.Count)"
if ($bad.Count -gt 0) {
    Write-Host "  $($bad.Count) FAILED VERIFICATION - re-run to retry them:"
    $bad | Select-Object -First 10 | ForEach-Object { Write-Host "    $_" }
}

Write-Host ''
Write-Host 'These blobs were overwritten, so the CDN must be purged or it keeps serving the old art:'
Write-Host '  az cdn endpoint purge -g Sharkingbird --profile-name PumpItUpScores -n piuscores --content-paths "/songs/*"'
Write-Host 'Then verify:'
Write-Host "  curl.exe -sI https://piuimages.arroweclip.se/$($changed[0].BlobPath) | findstr /i content-type"
