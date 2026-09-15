<#
.SYNOPSIS
    Stamps the real image content type on every piuimages blob that is still serving
    application/octet-stream.

.DESCRIPTION
    AzureBlobFileUploadClient set no content type until PR #168, so every blob uploaded
    before then serves application/octet-stream - which some unfurlers and crawlers refuse
    to render as an og:image. New uploads are fine; this fixes the backlog.

    It stamps properties rather than re-uploading bytes: Set Blob Properties changes the
    served Content-Type in one small request per blob, with no download, no re-upload and
    no risk of replacing good art with a bad fetch. The bytes on the CDN are already the
    right bytes - only the header was wrong.

    Idempotent: a blob whose type is already correct is skipped, so re-running after a
    partial failure costs nothing. Only mapped image extensions are touched.

.EXAMPLE
    .\stamp-content-types.ps1 -WhatIf
    Census only - lists what would change, writes nothing.

.EXAMPLE
    .\stamp-content-types.ps1
    Stamps, then prints the CDN purge command to run afterwards.
#>
[CmdletBinding()]
param(
    [switch]$WhatIf,
    [string]$Prefix = '',
    [int]$ThrottleMs = 0
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PiuImages.psm1') -Force

$account = Get-BlobAccount
Write-Host "account: $($account.Name)   container: `$web"

# List needs only 'rl'; the stamp needs 'w'. Letters stay in the service's canonical
# order (racwdxltmeop) or every signed request comes back 403.
$permissions = if ($WhatIf) { 'rl' } else { 'rwl' }
$sas = New-ContainerSas -Account $account -Permissions $permissions -Hours 6
$base = Get-ContainerBaseUrl -Account $account

Write-Host 'Listing blobs...'
$blobs = Get-BlobList -Account $account -Sas $sas -Prefix $Prefix
Write-Host "  $($blobs.Count) blobs found."

$work = @()
foreach ($b in $blobs) {
    $target = Get-ContentTypeForPath $b.Name
    if (-not $target) { continue }
    if ($b.Properties.'Content-Type' -eq $target) { continue }
    $work += [pscustomobject]@{
        Name = $b.Name
        Type = $target
        Md5  = $b.Properties.'Content-MD5'
    }
}

Write-Host ''
Write-Host "To stamp: $($work.Count)"
# Malformed names carry no '/' at all, so they would each become their own folder bucket
# and bury the real summary under 161 one-line groups.
$work | Group-Object { if ($_.Name -match '%2[fF]') { '(malformed names)' } else { ($_.Name -split '/')[0] } } |
    Sort-Object Count -Descending |
    ForEach-Object { Write-Host ("  {0,6}  {1}" -f $_.Count, $_.Name) }

# Blobs whose NAME contains a literal %2f are an orphaned set from an upload that escaped
# the whole path as one segment instead of per segment. They are unreachable duplicates of
# the real avatars/<file>.png blobs. Reported, never touched - deleting is the owner's call.
$malformed = @($blobs | Where-Object { $_.Name -match '%2[fF]' })
if ($malformed.Count -gt 0) {
    Write-Host ''
    Write-Host "NOTE: $($malformed.Count) blobs have a literal %2f in their name (e.g. $($malformed[0].Name))."
    Write-Host '      Those are orphans from an older upload bug, not served by any page. Left alone.'
}

if ($work.Count -eq 0) { Write-Host ''; Write-Host 'Nothing to do - every mapped blob already has its type.'; return }
if ($WhatIf) { Write-Host ''; Write-Host '-WhatIf: no changes made.'; return }

Write-Host ''
$done = 0
$failed = @()
foreach ($item in $work) {
    $url = "$base/$(ConvertTo-BlobPath $item.Name)?comp=properties&$sas"
    # Content-MD5 is re-sent where present so the stamp does not clear it. No blob in this
    # container carries cache-control or content-encoding, so nothing else is lost.
    $headers = @{
        'x-ms-version'            = '2022-11-02'
        'x-ms-blob-content-type'  = $item.Type
    }
    if ($item.Md5) { $headers['x-ms-blob-content-md5'] = $item.Md5 }
    try {
        # No -Body and no -ContentType: Set Blob Properties carries its values in headers
        # only, and an empty -ContentType is a parameter-binding hazard for no gain.
        Invoke-WebRequest -Method Put -Uri $url -Headers $headers -UseBasicParsing | Out-Null
        $done++
    }
    catch { $failed += "$($item.Name)  ->  $($_.Exception.Message)" }
    if ($done -gt 0 -and ($done % 250) -eq 0) { Write-Host "  $done / $($work.Count)" }
    if ($ThrottleMs -gt 0) { Start-Sleep -Milliseconds $ThrottleMs }
}

Write-Host ''
Write-Host "Stamped $done of $($work.Count). Failed: $($failed.Count)"
$failed | Select-Object -First 10 | ForEach-Object { Write-Host "  FAILED: $_" }

# Re-list and re-check rather than trusting the 200s. Set Blob Properties answers 200 for a
# request the service accepted, which is not the same claim as "the blob now serves this
# type" - and a silent no-op here is exactly the failure that would go unnoticed until an
# unfurler refused an image again months later.
Write-Host ''
Write-Host 'Verifying against a fresh listing...'
$after = Get-BlobList -Account $account -Sas $sas -Prefix $Prefix
$stillWrong = @($after | Where-Object {
        $t = Get-ContentTypeForPath $_.Name
        $t -and $_.Properties.'Content-Type' -ne $t
    })
if ($stillWrong.Count -eq 0) { Write-Host '  every mapped blob now serves its real image type.' }
else {
    Write-Host "  $($stillWrong.Count) blobs STILL have the wrong type - re-run to retry them."
    $stillWrong | Select-Object -First 5 | ForEach-Object { Write-Host "    $($_.Name)" }
}

Write-Host ''
Write-Host 'Last step - purge the CDN so cached octet-stream responses do not linger:'
Write-Host '  az cdn endpoint purge -g Sharkingbird --profile-name PumpItUpScores -n piuscores --content-paths "/*"'
Write-Host 'Purge takes ~2-10 min on Standard_Microsoft, then verify:'
Write-Host '  curl.exe -sI https://piuimages.arroweclip.se/songs/RushMore.png | findstr /i content-type'
