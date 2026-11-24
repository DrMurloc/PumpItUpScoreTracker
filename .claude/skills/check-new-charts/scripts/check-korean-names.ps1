# check-new-charts step 4: the Korean title piugame serves for each song about to ship.
#
# koreanName is import-critical: a ko-KR row that differs from what a Korean piugame session
# serves silently drops the song from those players' imports. The batch builder takes it from the
# YouTube titles, which are usually right but not always, so read the site itself before shipping.
#
# Signs in with the owner-authorized PiuTest account (NEVER print the secret values), switches the
# session to Korean, reads each song's board page (over_ranking_view.php?no=; the no= ids in
# catalog.csv are stable across languages), and ALWAYS restores English in a finally block,
# confirming the page says lang="en" - the account's English resting state is what the
# English-parsing probes rely on.
param(
    [string]$StateDir = "$env:USERPROFILE\.piu-score-tracker\check-new-charts",
    [string]$BatchJson,   # a built batch: checks its songs' koreanName. Default: every unshipped oracle song
    [string]$BaseUrl = 'https://piugame.com'
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0'

# ---- the songs to check, and the Korean name the batch carries for each (if any) ----
$expected = [ordered]@{}
if ($BatchJson) {
    foreach ($s in (Get-Content $BatchJson -Raw -Encoding UTF8 | ConvertFrom-Json).songs) { $expected[[string]$s.name] = [string]$s.koreanName }
}
else {
    $shipped = @{}
    $shippedPath = Join-Path $StateDir 'shipped.json'
    if (Test-Path $shippedPath) { foreach ($s in (Get-Content $shippedPath -Raw -Encoding UTF8 | ConvertFrom-Json).songs) { $shipped[[string]$s.name] = $true } }
    foreach ($s in (Get-Content (Join-Path $StateDir 'oracle.json') -Raw -Encoding UTF8 | ConvertFrom-Json).songs) {
        if (-not $shipped.ContainsKey([string]$s.name)) { $expected[[string]$s.name] = $null }
    }
}
if ($expected.Count -eq 0) { Write-Output 'no songs to check'; return }

$catalog = @(Import-Csv (Join-Path $StateDir 'catalog.csv') -Encoding UTF8)
$boardIds = [ordered]@{}
foreach ($name in $expected.Keys) {
    $row = @($catalog | Where-Object { $_.Name -eq $name }) | Select-Object -First 1
    if ($null -eq $row) { Write-Output "NO BOARD ROW [$name] - not in catalog.csv (run rebuild-oracle.ps1?)"; continue }
    $boardIds[$name] = $row.Id
}
if ($boardIds.Count -eq 0) { Write-Output 'none of the songs is on the boards - nothing to read'; return }

# ---- sign in, read in Korean, always restore English ----
$sec = Get-Content "$env:APPDATA\Microsoft\UserSecrets\2957ac5b-8cbb-49cf-be12-3b607ea9b818\secrets.json" -Raw | ConvertFrom-Json
$user = $sec.'PiuTest:Username'; $pass = $sec.'PiuTest:Password'
if (-not $user) { $user = $sec.PiuTest.Username; $pass = $sec.PiuTest.Password }
if (-not $user) { throw 'no PiuTest creds in the user-secrets store' }
$sess = $null
try { Invoke-WebRequest -Uri "$BaseUrl/" -SessionVariable sess -UserAgent $ua -TimeoutSec 15 -UseBasicParsing | Out-Null } catch { if ($null -eq $sess) { $sess = New-Object Microsoft.PowerShell.Commands.WebRequestSession } }
Invoke-WebRequest -Uri "$BaseUrl/bbs/login_check.php" -Method Post -Body @{ url = '/'; mb_id = $user; mb_password = $pass } -WebSession $sess -UserAgent $ua -TimeoutSec 20 -UseBasicParsing | Out-Null

function Read-BoardTitle([string]$no) {
    $r = Invoke-WebRequest -Uri "$BaseUrl/leaderboard/over_ranking_view.php?no=$no" -WebSession $sess -UserAgent $ua -TimeoutSec 30 -UseBasicParsing
    return [pscustomobject]@{
        Lang  = [regex]::Match($r.Content, '<html[^>]*\slang="([^"]+)"').Groups[1].Value
        Title = [System.Net.WebUtility]::HtmlDecode([regex]::Match($r.Content, 'class="songName_w"><p class="tt">([^<]*)<').Groups[1].Value).Trim()
    }
}

$site = [ordered]@{}
try {
    Invoke-WebRequest -Uri "$BaseUrl/ajax/language_update.php" -Method Post -Body @{ lang = 'kr' } -WebSession $sess -UserAgent $ua -TimeoutSec 15 -UseBasicParsing | Out-Null
    foreach ($name in $boardIds.Keys) {
        $page = Read-BoardTitle $boardIds[$name]
        if ($page.Lang -ne 'ko') { throw "the session did not switch to Korean (page lang='$($page.Lang)')" }
        $site[$name] = $page.Title
        Start-Sleep -Milliseconds 400
    }
}
finally {
    Invoke-WebRequest -Uri "$BaseUrl/ajax/language_update.php" -Method Post -Body @{ lang = 'en' } -WebSession $sess -UserAgent $ua -TimeoutSec 15 -UseBasicParsing | Out-Null
    $check = Read-BoardTitle @($boardIds.Values)[0]
    if ($check.Lang -ne 'en') { Write-Output "WARNING: the PiuTest session still reads lang='$($check.Lang)' - restore English by hand" }
    else { Write-Output 'English restored' }
}

# ---- report ----
$mismatches = 0
foreach ($name in $site.Keys) {
    $want = $expected[$name]
    if ($null -eq $want) { Write-Output "SITE      [$name] -> [$($site[$name])]" }
    elseif ([string]::Equals($want, $site[$name], [StringComparison]::Ordinal)) { Write-Output "OK        [$name] -> [$($site[$name])]" }
    else { $mismatches++; Write-Output "MISMATCH  [$name] batch [$want] site [$($site[$name])] -- use the site's" }
}
Write-Output "checked $($site.Count) songs, $mismatches mismatches"
