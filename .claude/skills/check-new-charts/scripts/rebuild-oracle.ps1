# check-new-charts step 1 (when needed): rebuild the site-truth oracle from piugame.com.
#
# Sweeps the over-20 leaderboard filters (the only public P2 catalog surface), rotates the
# catalog checkpoint, diffs against the previous sweep, and appends genuinely-new songs to
# oracle.json with canonical name/image/chart-list (koreanName/artist/stepArtist/youtubeHash
# stay TODO until the YouTube pass fills them).
#
# CANDIDATES REQUIRE REVIEW: a song new to the SITE is not necessarily new to the TRACKER -
# returning songs (cut in Phoenix, back in Phoenix 2, e.g. KUGUTSU) already exist and go the
# ChartMix-seed path, NOT BulkAddCharts. The agent verifies each candidate before shipping it.
#
# Login uses the owner-authorized PiuTest account from the shared AppHost user-secrets store.
# NEVER print or persist the secret values.
param(
    [string]$StateDir = "$env:USERPROFILE\.piu-score-tracker\check-new-charts",
    [string]$BaseUrl = 'https://piugame.com'
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0'
New-Item -ItemType Directory -Force $StateDir | Out-Null

$sec = Get-Content "$env:APPDATA\Microsoft\UserSecrets\2957ac5b-8cbb-49cf-be12-3b607ea9b818\secrets.json" -Raw | ConvertFrom-Json
$user = $sec.'PiuTest:Username'; $pass = $sec.'PiuTest:Password'
if (-not $user) { $user = $sec.PiuTest.Username; $pass = $sec.PiuTest.Password }
if (-not $user) { throw 'no PiuTest creds in the user-secrets store' }

# P2 leaderboards are login-gated (anonymous traffic gets a marketing shell with 0 rows)
$sess = $null
try { Invoke-WebRequest -Uri "$BaseUrl/" -SessionVariable sess -UserAgent $ua -TimeoutSec 15 -UseBasicParsing | Out-Null } catch { if ($null -eq $sess) { $sess = New-Object Microsoft.PowerShell.Commands.WebRequestSession } }
Invoke-WebRequest -Uri "$BaseUrl/bbs/login_check.php" -Method Post -Body @{ url = '/'; mb_id = $user; mb_password = $pass } -WebSession $sess -UserAgent $ua -TimeoutSec 20 -UseBasicParsing | Out-Null
# song names follow the SESSION language, not Accept-Language; the oracle needs canonical
# English, and en is also the account's required resting state (English-parsing tests use it)
try { Invoke-WebRequest -Uri "$BaseUrl/ajax/language_update.php" -Method Post -Body @{ lang = 'en' } -WebSession $sess -UserAgent $ua -TimeoutSec 15 -UseBasicParsing | Out-Null } catch { Write-Output "language_update en failed ($($_.Exception.Message)) - names may come back Korean, verify the catalog" }

function Parse-Rows([string]$html) {
    # plain return: callers collect with @(...) - `return ,$rows` would smuggle the whole
    # page array through as ONE element and ConvertTo-Csv serializes array metadata
    $rows = @()
    foreach ($seg in ($html -split 'class="li_in"' | Select-Object -Skip 1)) {
        $id = [regex]::Match($seg, 'over_ranking_view\.php\?no=([A-Za-z0-9+/=%]+)')
        $tt = [regex]::Match($seg, 'class="tt">([^<]+)<')
        # stepball art carries type+level: <t>_bg.png + <t>_num_<d>.png digits in order
        # (P2 serves them from l_img/p2/stepball/ - the regex tolerates both hosts' paths)
        $ty = [regex]::Match($seg, '([sdc])_bg\.png')
        $digits = @([regex]::Matches($seg, '_num_(\d)\.png') | ForEach-Object { $_.Groups[1].Value })
        if ($id.Success -and $tt.Success) {
            $lvl = ''
            if ($digits.Count -gt 0) { $lvl = -join $digits }
            $rows += ,([pscustomobject]@{
                Name  = [System.Net.WebUtility]::HtmlDecode($tt.Groups[1].Value).Trim()
                Type  = $ty.Groups[1].Value
                Level = $lvl
                Id    = $id.Groups[1].Value
            })
        }
    }
    return $rows
}

# ---- sweep: iterate the lv FILTERS (empty lv = capped top-80 "All" view, useless) ----
$levels = @('20', '21', '22', '23', '24', '25', '26', '27over', 'coop')
$all = @()
$seen = @{}
$failedLevels = @()
foreach ($lv in $levels) {
    $lvCount = 0
    $ok = $true
    for ($p = 1; $p -le 80; $p++) {
        $rows = @()
        foreach ($attempt in 1..3) {
            try {
                $r = Invoke-WebRequest -Uri "$BaseUrl/leaderboard/over_ranking.php?lv=$lv&search=&&page=$p" -WebSession $sess -UserAgent $ua -TimeoutSec 12 -UseBasicParsing
                $rows = @(Parse-Rows $r.Content)
            } catch { $rows = @() }
            if ($rows.Count -gt 0) { break }
            Start-Sleep -Milliseconds (400 * $attempt)
        }
        if ($rows.Count -eq 0) {
            if ($p -eq 1) { $ok = $false }   # a filter with zero page-1 rows = bounce/limit, not "end"
            break
        }
        # the board serves REPEATED content for out-of-range pages - stop when nothing new
        $fresh = @($rows | Where-Object { -not $seen.ContainsKey("$($_.Id)|$($_.Type)|$($_.Level)") })
        if ($fresh.Count -eq 0) { break }
        foreach ($f in $fresh) { $seen["$($f.Id)|$($f.Type)|$($f.Level)"] = $true }
        $all += $fresh
        $lvCount += $fresh.Count
        Start-Sleep -Milliseconds 400
    }
    if ($ok) { Write-Output "lv=$lv : $lvCount rows" } else { $failedLevels += $lv; Write-Output "lv=$lv : FAILED (page1 empty after retries)" }
}
if ($failedLevels.Count -gt 0) { throw "sweep incomplete (failed filters: $($failedLevels -join ', ')) - catalog NOT rotated; check login/session and rerun" }
$badRows = @($all | Where-Object { $_.Type -eq '' -or $_.Level -eq '' })
foreach ($b in $badRows) { Write-Output "FLAG unparsed stepball (?? level art like 1948 D29?): [$($b.Name)] type='$($b.Type)' level='$($b.Level)' id=$($b.Id)" }

# ---- rotate the catalog checkpoint and diff ----
$catalogPath = Join-Path $StateDir 'catalog.csv'
$prevNames = @{}
if (Test-Path $catalogPath) {
    foreach ($row in (Import-Csv $catalogPath)) { $prevNames[$row.Name] = $true }
    Copy-Item $catalogPath (Join-Path $StateDir 'catalog-prev.csv') -Force
}
$all | Select-Object Name, Type, Level, Id | ConvertTo-Csv -NoTypeInformation | Out-File $catalogPath -Encoding UTF8
Write-Output "catalog: $($all.Count) rows, $(($all | Group-Object Name).Count) songs -> $catalogPath"

$newNames = @(($all | Group-Object Name | ForEach-Object { $_.Name }) | Where-Object { $prevNames.Count -gt 0 -and -not $prevNames.ContainsKey($_) })
if ($prevNames.Count -eq 0) { Write-Output 'no previous catalog - first sweep is the baseline, no candidates derived' }

# ---- append genuinely-new candidates to the oracle ----
$oraclePath = Join-Path $StateDir 'oracle.json'
$oracleSongs = @()
if (Test-Path $oraclePath) { $oracleSongs = @((Get-Content $oraclePath -Raw -Encoding UTF8 | ConvertFrom-Json).songs) }
$oracleNames = @{}
foreach ($o in $oracleSongs) { $oracleNames[[string]$o.name] = $true }
$shippedNames = @{}
$shippedPath = Join-Path $StateDir 'shipped.json'
if (Test-Path $shippedPath) { foreach ($s in (Get-Content $shippedPath -Raw -Encoding UTF8 | ConvertFrom-Json).songs) { $shippedNames[[string]$s.name] = $true } }

$typeMap = @{ s = 'Single'; d = 'Double'; c = 'CoOp' }
$added = @()
foreach ($name in $newNames) {
    if ($oracleNames.ContainsKey($name) -or $shippedNames.ContainsKey($name)) { continue }
    $rows = @($all | Where-Object { $_.Name -eq $name })

    $img = 'TODO'
    try {
        $page = Invoke-WebRequest -Uri "$BaseUrl/leaderboard/over_ranking_view.php?no=$($rows[0].Id)" -WebSession $sess -UserAgent $ua -TimeoutSec 30 -UseBasicParsing
        $m = [regex]::Match($page.Content, 'song_img2/[a-f0-9]+\.png')
        if ($m.Success) { $img = "$BaseUrl/data/" + $m.Value }
    } catch { Write-Output "IMGFAIL: $name" }

    $songType = 'Arcade'
    if ($name -match 'SHORT CUT') { $songType = 'ShortCut' }
    elseif ($name -match 'FULL SONG') { $songType = 'FullSong' }
    elseif ($name -match 'Remix') { $songType = 'Remix' }

    $charts = @($rows | Where-Object { $_.Level -ne '' } | Sort-Object { $typeMap[$_.Type] }, { [int]$_.Level } | ForEach-Object {
        [ordered]@{ type = $typeMap[$_.Type]; level = [int]$_.Level; stepArtist = 'TODO'; youtubeHash = 'TODO' }
    })

    $oracleSongs += ,([ordered]@{
        name            = $name
        koreanName      = 'TODO'
        artist          = 'TODO'
        type            = $songType
        minBpm          = 1
        maxBpm          = 1
        durationSeconds = 105
        imageUrl        = $img
        charts          = $charts
    })
    $added += $name
    Start-Sleep -Milliseconds 350
}

# existing unshipped oracle entries whose site chart set GREW (new chart on a pending song):
# append the missing rows so the completeness gate demands their videos too
foreach ($o in $oracleSongs) {
    $oName = [string]$o.name
    if ($shippedNames.ContainsKey($oName) -or $added -contains $oName) { continue }
    $siteRows = @($all | Where-Object { $_.Name -eq $oName -and $_.Level -ne '' })
    if ($siteRows.Count -eq 0) { continue }
    $have = @($o.charts | ForEach-Object { "$($_.type)|$([int]$_.level)" })
    foreach ($rowKey in ($siteRows | ForEach-Object { "$($typeMap[$_.Type])|$([int]$_.Level)" })) {
        if ($have -notcontains $rowKey) {
            $parts = $rowKey -split '\|'
            $o.charts = @($o.charts) + ,([ordered]@{ type = $parts[0]; level = [int]$parts[1]; stepArtist = 'TODO'; youtubeHash = 'TODO' })
            Write-Output "ORACLE UPDATED [$oName]: site grew chart $rowKey - added"
        }
    }
}

[IO.File]::WriteAllText($oraclePath, ([ordered]@{ songs = $oracleSongs } | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($true)))
Write-Output "oracle: $($oracleSongs.Count) songs -> $oraclePath"
Write-Output ''
if ($added.Count -gt 0) {
    Write-Output "=== NEW CANDIDATES ($($added.Count)) - VERIFY EACH IS NEW TO THE TRACKER (not a returning song) BEFORE SHIPPING ==="
    foreach ($a in $added) { Write-Output "  $a" }
}
else { Write-Output 'no new site songs since the previous sweep' }
