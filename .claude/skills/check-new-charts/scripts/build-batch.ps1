# check-new-charts step 3: build the /Admin/BulkAddCharts JSON batch from the cached watch pages.
#
# Scans the WHOLE video cache (not just the last window) so a song whose chart videos arrive
# across several weeks completes automatically once its last video lands. Songs already emitted
# in a previous batch (shipped.json) are excluded - with a loud flag if their chart set changed.
#
# Schema contract: docs/design/new-charts-json.md. Validation problems block the JSON write.
param(
    [string]$StateDir = "$env:USERPROFILE\.piu-score-tracker\check-new-charts",
    [string]$OutDir = "$env:USERPROFILE\Downloads"
)
$ErrorActionPreference = 'Stop'
$stamp = Get-Date -Format 'yyyy-MM-dd'
$outJson = Join-Path $OutDir "phoenix2-batch-$stamp.json"
$outReport = Join-Path $OutDir "phoenix2-batch-$stamp-report.txt"
# hangul range from char codes: hangul literals in BOM-less .ps1 files are read as ANSI by
# PS 5.1 and silently mangle - never put them in source
$hg = '[' + [char]0xAC00 + '-' + [char]0xD7A3 + ']'

function Normalize([string]$s) {
    if ($null -eq $s) { return '' }
    ($s -replace [char]0x2019, "'" -replace '\s+', ' ').Trim().ToUpperInvariant()
}

# some historical oracle blobs carried strings serialized as {"value": "..."} objects
# (Get-Content note-property leak) - unwrap defensively
function BlobString($v) {
    if ($v -is [string]) { return $v }
    if ($null -ne $v -and $v.PSObject.Properties['value']) { return [string]$v.value }
    return [string]$v
}

# ---- load the oracle (canonical names, images, types, >=20 chart lists from the site) ----
$oraclePath = Join-Path $StateDir 'oracle.json'
if (-not (Test-Path $oraclePath)) { throw "no $oraclePath - run rebuild-oracle.ps1 first (see SKILL.md)" }
$blob = Get-Content $oraclePath -Raw -Encoding UTF8 | ConvertFrom-Json
$siteSongs = @()
foreach ($s in $blob.songs) {
    $n = BlobString $s.name
    $siteSongs += [pscustomobject]@{
        Name     = $n
        Norm     = Normalize $n
        Type     = BlobString $s.type
        ImageUrl = BlobString $s.imageUrl
        Site20   = @($s.charts | ForEach-Object { "$(BlobString $_.type)|$([int]$_.level)" })
    }
}

# ---- load already-shipped songs (previous batches) ----
$shippedPath = Join-Path $StateDir 'shipped.json'
$shippedCharts = @{}
if (Test-Path $shippedPath) {
    $shipped = Get-Content $shippedPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($ss in $shipped.songs) { $shippedCharts[(Normalize $ss.name)] = @($ss.charts | Sort-Object) -join ',' }
}

# ---- load fetched videos ----
$videos = @()
foreach ($f in Get-ChildItem (Join-Path $StateDir 'videos\*.json')) {
    $videos += (Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json)
}
$expectChannelId = $null
$statePath = Join-Path $StateDir 'state.json'
if (Test-Path $statePath) { $expectChannelId = (Get-Content $statePath -Raw -Encoding UTF8 | ConvertFrom-Json).channelId }

$report = New-Object System.Collections.ArrayList
# Write-Host, NOT Write-Output: Note is called inside functions whose return values
# would otherwise absorb the message (PS returns all pipeline output)
function Note([string]$line) { [void]$report.Add($line); Write-Host $line }

Note "videos in cache: $($videos.Count)"

# ---- classify + parse ----
function DescField([string]$desc, [string]$field) {
    $m = [regex]::Match($desc, "(?m)^\s*$field\s*:\s*(.+?)\s*$")
    if ($m.Success) { return $m.Groups[1].Value.Trim() } else { return $null }
}

function ParseChartCodes([string]$raw) {
    # "D24" | "S17, S20" | "CO-OP x2" -> list of @{Type;Level}; unparseable tokens returned as strings
    $out = @()
    foreach ($tok in ($raw -split ',')) {
        $t = $tok.Trim()
        $m = [regex]::Match($t, '^(?i)(S|D)(\d{1,2})$')
        if ($m.Success) {
            $type = 'Single'
            if ($m.Groups[1].Value -match '(?i)d') { $type = 'Double' }
            $out += ,([pscustomobject]@{ Type = $type; Level = [int]$m.Groups[2].Value })
            continue
        }
        $c = [regex]::Match($t, '^(?i)CO-?OP\s*[xX]?\s*(\d)$')
        if ($c.Success) {
            $out += ,([pscustomobject]@{ Type = 'CoOp'; Level = [int]$c.Groups[1].Value })
            continue
        }
        $out += ,$t
    }
    return $out
}

function ParseBpm([string]$raw) {
    $nums = [regex]::Matches($raw, '\d+(?:\.\d+)?') | ForEach-Object { [decimal]$_.Value }
    if ($nums.Count -eq 0) { return $null }
    $sorted = $nums | Sort-Object
    return @{ Min = $sorted[0]; Max = $sorted[-1] }
}

# song accumulator keyed by canonical site name
$songData = @{}
$unmatched = @()
$skipped = @()

foreach ($v in $videos) {
    if ($expectChannelId -and $v.channelId -ne $expectChannelId) { $skipped += "WRONG CHANNEL: $($v.videoId) $($v.title)"; continue }
    $desc = $v.description
    if ($null -eq $desc) { $desc = '' }
    $titleField = DescField $desc 'Title'
    $isBga = ($v.title -match '\]\s*BGA') -or ($desc -match '(?m)^\s*(Illustrator|Visualizer)\s*:')
    $stepChart = DescField $desc 'Step Chart'
    $isChart = -not $isBga -and $null -ne $stepChart

    if (-not $isBga -and -not $isChart) { $skipped += "OTHER: $($v.videoId) $($v.title)"; continue }
    if ($null -eq $titleField) { $unmatched += "NO TITLE FIELD: $($v.videoId) $($v.title)"; continue }

    # canonical song match: oracle name with the longest normalized prefix of the Title field
    $normTitle = Normalize $titleField
    $match = $null
    $krFromParen = $null
    foreach ($s in ($siteSongs | Sort-Object { $_.Norm.Length } -Descending)) {
        if ($normTitle -eq $s.Norm -or $normTitle.StartsWith($s.Norm + ' ')) { $match = $s; break }
    }
    if ($null -eq $match) {
        # fallback: songs with subtitles get the Korean annotation MID-title
        # ("INFiNiTE ENERZY (X) -Overdoze-", "Pull me up (X) Feat. Monya");
        # strip the hangul paren group and compare paren-insensitively
        $hangulParen = [regex]::Match($titleField, "\(([^()]*$hg[^()]*)\)")
        $stripped = [regex]::Replace($titleField, "\s*\([^()]*$hg[^()]*\)", ' ')
        $normStripped = (Normalize $stripped) -replace '[()]', ''
        if ($normStripped -ne '') {
            foreach ($s in ($siteSongs | Sort-Object { $_.Norm.Length } -Descending)) {
                if ($normStripped -eq ($s.Norm -replace '[()]', '')) {
                    $match = $s
                    if ($hangulParen.Success) { $krFromParen = $hangulParen.Groups[1].Value.Trim() }
                    if (-not $songData.ContainsKey($s.Name)) { Note "FLAG [$($s.Name)] matched via mid-title Korean annotation: [$titleField]" }
                    break
                }
            }
        }
    }
    if ($null -eq $match) { $unmatched += "NO BLOB MATCH: $($v.videoId) [$titleField] $($v.title)"; continue }

    if ($null -ne $krFromParen) {
        $kr = $krFromParen
    }
    else {
        # korean name = remainder after the english name, outer parens stripped
        $remainder = $titleField.Substring([math]::Min($match.Name.Length, $titleField.Length)).Trim()
        $kr = $remainder -replace '^\(', '' -replace '\)$', ''
        $kr = $kr.Trim()
        if ($kr -eq '') { $kr = $match.Name }
    }

    if (-not $songData.ContainsKey($match.Name)) {
        $songData[$match.Name] = [pscustomobject]@{
            Site      = $match
            Artists   = New-Object System.Collections.ArrayList
            Bpms      = New-Object System.Collections.ArrayList
            KrNames   = New-Object System.Collections.ArrayList
            BgaLength = $null
            Charts    = New-Object System.Collections.ArrayList  # Type|Level -> per-chart record
        }
    }
    $agg = $songData[$match.Name]

    $artist = DescField $desc 'Artist'
    if ($artist) { [void]$agg.Artists.Add($artist) }
    $bpmRaw = DescField $desc 'BPM'
    if ($bpmRaw) { $b = ParseBpm $bpmRaw; if ($b) { [void]$agg.Bpms.Add($b) } }
    [void]$agg.KrNames.Add($kr)

    if ($isBga) {
        $agg.BgaLength = $v.lengthSeconds
        continue
    }

    $stepArtist = DescField $desc 'Step Artist'
    $parsed = @(ParseChartCodes $stepChart)
    $codes = @($parsed | Where-Object { $_ -isnot [string] })
    foreach ($bt in @($parsed | Where-Object { $_ -is [string] })) { $unmatched += "UNPARSED CHART TOKEN '$bt': $($v.videoId) $($v.title)" }

    # cross-check against the title's trailing chart codes - Andamiro typos descriptions
    # (e.g. T.B.H "S2, S4" titled video with "Step Chart : S2, S24"); title wins on mismatch
    $tsm = [regex]::Match($v.title, '((?:(?:S|D)\d{1,2}|CO-?OP\s*[xX]?\s*\d)(?:\s*,\s*(?:(?:S|D)\d{1,2}|CO-?OP\s*[xX]?\s*\d))*)\s*$', 'IgnoreCase')
    if ($tsm.Success) {
        $titleCodes = @(ParseChartCodes $tsm.Groups[1].Value | Where-Object { $_ -isnot [string] })
        $descSet = @($codes | ForEach-Object { "$($_.Type)|$($_.Level)" } | Sort-Object) -join ','
        $titleSet = @($titleCodes | ForEach-Object { "$($_.Type)|$($_.Level)" } | Sort-Object) -join ','
        if ($titleCodes.Count -gt 0 -and $descSet -ne $titleSet) {
            Note "FLAG [$($match.Name)] title/description chart codes disagree on $($v.videoId): title [$titleSet] vs desc [$descSet] -- using title"
            $codes = $titleCodes
        }
    }

    # split step artists across a multi-chart video only when counts line up
    $stepArtists = @()
    if ($stepArtist -and $stepArtist.Contains('/')) {
        $parts = @($stepArtist -split '/' | ForEach-Object { $_.Trim() })
        if ($parts.Count -eq $codes.Count) { $stepArtists = $parts }
        else { $unmatched += "STEP ARTIST SPLIT MISMATCH '$stepArtist' vs $($codes.Count) charts: $($v.videoId)" }
    }

    for ($i = 0; $i -lt $codes.Count; $i++) {
        $sa = $stepArtist
        if ($stepArtists.Count -gt 0) { $sa = $stepArtists[$i] }
        [void]$agg.Charts.Add([pscustomobject]@{
            Type       = $codes[$i].Type
            Level      = $codes[$i].Level
            StepArtist = $sa
            Hash       = $v.videoId
            Length     = $v.lengthSeconds
        })
    }
}

# ---- per-song consolidation + completeness ----
function Consensus($list, [string]$label, [string]$song) {
    $grouped = @($list | Group-Object | Sort-Object Count -Descending)
    if ($grouped.Count -gt 1) { Note "FLAG [$song] $label disagrees across videos: $(($grouped | ForEach-Object { $_.Name + ' x' + $_.Count }) -join '; ') -- using most common" }
    return $grouped[0].Name
}

$included = @()
$deferred = @()
$problems = @()

foreach ($name in ($songData.Keys | Sort-Object)) {
    $agg = $songData[$name]
    $site = $agg.Site

    # dedupe charts by Type|Level
    $chartMap = [ordered]@{}
    foreach ($c in $agg.Charts) {
        $key = "$($c.Type)|$($c.Level)"
        if ($chartMap.Contains($key)) { Note "FLAG [$name] duplicate chart $key across videos (keeping first)" }
        else { $chartMap[$key] = $c }
    }

    $missing = @($site.Site20 | Where-Object { -not $chartMap.Contains($_) })
    if ($missing.Count -gt 0) {
        $deferred += "$name -- site charts not yet on YouTube: $($missing -join ', ') (have $($chartMap.Count) videos-charts)"
        continue
    }

    # the site's over-20 list is complete: a YouTube-only chart at >=20 is a phantom
    # (typo that survived title/desc cross-check) or a real site-sweep gap - block either way
    foreach ($key in $chartMap.Keys) {
        $lvl = [int]($key -split '\|')[1]
        if ($lvl -ge 20 -and $chartMap[$key].Type -ne 'CoOp' -and $site.Site20 -notcontains $key) {
            $problems += "$name : YouTube chart $key is >=20 but absent from the site's over-20 list -- verify before shipping"
        }
    }

    $artist = Consensus $agg.Artists 'artist' $name
    $kr = Consensus $agg.KrNames 'koreanName' $name
    $bpmMin = ($agg.Bpms | ForEach-Object { $_.Min } | Sort-Object | Select-Object -First 1)
    $bpmMax = ($agg.Bpms | ForEach-Object { $_.Max } | Sort-Object | Select-Object -Last 1)
    $duration = $agg.BgaLength
    if ($null -eq $duration) {
        $duration = ($agg.Charts | ForEach-Object { $_.Length } | Sort-Object | Select-Object -First 1)
        Note "FLAG [$name] no BGA video -- duration from shortest chart video ($duration s)"
    }

    $charts = @()
    $typeOrder = @{ Single = 0; Double = 1; CoOp = 2 }
    foreach ($c in ($chartMap.Values | Sort-Object { $typeOrder[$_.Type] }, Level)) {
        $chart = [ordered]@{
            type        = $c.Type
            level       = $c.Level
            stepArtist  = $c.StepArtist
            youtubeHash = $c.Hash
        }
        $charts += ,$chart
    }

    $included += ,([ordered]@{
        name            = $site.Name
        koreanName      = $kr
        artist          = $artist
        type            = $site.Type
        minBpm          = $bpmMin
        maxBpm          = $bpmMax
        durationSeconds = [int]$duration
        imageUrl        = $site.ImageUrl
        charts          = $charts
    })
}

# ---- exclude songs already shipped in a previous batch ----
# (chart-set drift on a shipped song = a new video since shipping - the tool skips
# already-in-P2 songs on Confirm, so a drifted song needs MANUAL chart addition; flag loudly)
$previouslyShipped = @()
$kept = @()
foreach ($s in $included) {
    $norm = Normalize $s.name
    if ($shippedCharts.ContainsKey($norm)) {
        $nowSet = @($s.charts | ForEach-Object { "$($_.type)|$($_.level)" } | Sort-Object) -join ','
        if ($nowSet -ne $shippedCharts[$norm]) {
            Note "FLAG [$($s.name)] shipped in a previous batch but chart set CHANGED since: was [$($shippedCharts[$norm])] now [$nowSet] -- excluded from JSON, needs manual handling"
        }
        $previouslyShipped += $s.name
    }
    else { $kept += ,$s }
}
$included = $kept

# oracle songs with no videos at all (not yet uploaded by Andamiro)
$shippedNorms = @($shippedCharts.Keys)
$noVideos = @($siteSongs | Where-Object { -not $songData.ContainsKey($_.Name) -and $shippedNorms -notcontains $_.Norm } | ForEach-Object { $_.Name })

# ---- validation gate ----
foreach ($s in $included) {
    if ($s.koreanName -match 'TODO' -or [string]::IsNullOrWhiteSpace($s.koreanName)) { $problems += "$($s.name): bad koreanName" }
    if ([string]::IsNullOrWhiteSpace($s.artist)) { $problems += "$($s.name): missing artist" }
    if ($null -eq $s.minBpm -or $s.minBpm -le 0 -or $s.maxBpm -lt $s.minBpm) { $problems += "$($s.name): bad bpm $($s.minBpm)-$($s.maxBpm)" }
    if ($s.durationSeconds -le 0 -or $s.durationSeconds -gt 3600) { $problems += "$($s.name): bad duration $($s.durationSeconds)" }
    if ($s.charts.Count -eq 0) { $problems += "$($s.name): zero charts" }
    foreach ($c in $s.charts) {
        if ([string]::IsNullOrWhiteSpace($c.stepArtist)) { $problems += "$($s.name) $($c.type)$($c.level): missing stepArtist" }
        if ($c.youtubeHash -notmatch '^[A-Za-z0-9_-]{11}$') { $problems += "$($s.name) $($c.type)$($c.level): bad hash" }
    }
}

Note ''
Note "=== INCLUDED ($($included.Count) songs, $(($included | ForEach-Object { $_.charts.Count } | Measure-Object -Sum).Sum) charts) ==="
foreach ($s in $included) { Note ("  {0}  [{1}]  {2} charts  {3}-{4}bpm  {5}s  by {6}" -f $s.name, $s.koreanName, $s.charts.Count, $s.minBpm, $s.maxBpm, $s.durationSeconds, $s.artist) }
Note ''
Note "=== PREVIOUSLY SHIPPED ($($previouslyShipped.Count)) - excluded from this JSON ==="
foreach ($p in $previouslyShipped) { Note "  $p" }
Note ''
Note "=== DEFERRED - incomplete uploads ($($deferred.Count)) ==="
foreach ($d in $deferred) { Note "  $d" }
Note ''
Note "=== NO VIDEOS YET ($($noVideos.Count)) ==="
foreach ($n in $noVideos) { Note "  $n" }
Note ''
Note "=== UNMATCHED / ODD VIDEOS ($($unmatched.Count)) ==="
foreach ($u in $unmatched) { Note "  $u" }
Note ''
Note "=== SKIPPED NON-CHART/BGA VIDEOS ($($skipped.Count)) ==="
foreach ($k in $skipped) { Note "  $k" }
Note ''
if ($problems.Count -gt 0) {
    Note "=== VALIDATION PROBLEMS ($($problems.Count)) -- JSON NOT WRITTEN ==="
    foreach ($p in $problems) { Note "  $p" }
}
elseif ($included.Count -eq 0) {
    Note 'NOTHING NEW TO SHIP - JSON not written'
}
else {
    $json = [ordered]@{ songs = $included } | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText($outJson, $json, (New-Object System.Text.UTF8Encoding($true)))
    Note "WROTE $outJson"

    # record the emitted songs so the next run excludes them (if the owner decides NOT to
    # upload this batch, remove its entries from shipped.json or they will never re-emit)
    $shippedList = @()
    if (Test-Path $shippedPath) { $shippedList = @((Get-Content $shippedPath -Raw -Encoding UTF8 | ConvertFrom-Json).songs) }
    foreach ($s in $included) {
        $shippedList += ,([ordered]@{
            name    = $s.name
            charts  = @($s.charts | ForEach-Object { "$($_.type)|$($_.level)" } | Sort-Object)
            batch   = Split-Path $outJson -Leaf
            dateUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        })
    }
    [IO.File]::WriteAllText($shippedPath, ([ordered]@{ songs = $shippedList } | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    Note "shipped.json updated (+$($included.Count) songs)"
}
[IO.File]::WriteAllLines($outReport, $report, (New-Object System.Text.UTF8Encoding($false)))
Note "WROTE $outReport"
