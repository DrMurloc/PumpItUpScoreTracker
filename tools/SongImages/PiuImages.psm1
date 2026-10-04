# Shared helpers for the piuimages CDN maintenance scripts.
#
# Why no `az` anywhere: `az storage container generate-sas` writes a multi-line WARNING to
# stderr whenever credentials aren't passed inline ("we will query for account key..."), and
# Windows PowerShell 5.1 turns a native command's stderr into a NativeCommandError. Under the
# $ErrorActionPreference='Stop' these scripts need, that WARNING is fatal - the predecessor
# script died on its SAS line every run, before touching a single blob, which is what "it kept
# erroring" was. The account key is already in the AppHost user-secrets store, so the SAS is
# minted locally with HMAC and az never runs.

Set-StrictMode -Version Latest

$script:UserSecretsPath = "$env:APPDATA\Microsoft\UserSecrets\2957ac5b-8cbb-49cf-be12-3b607ea9b818\secrets.json"

function Get-PiuSecret {
    param([Parameter(Mandatory)][string]$Key)
    if (-not (Test-Path $script:UserSecretsPath)) { throw "user-secrets store not found: $script:UserSecretsPath" }
    $json = Get-Content $script:UserSecretsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $value = $json.PSObject.Properties | Where-Object { $_.Name -eq $Key } | Select-Object -First 1
    if (-not $value) { return $null }
    return $value.Value
}

# The stored key is spelled 'AzureBlob:ConnectionSTring' (sic) - the app binds it
# case-insensitively, so the typo is load-bearing in the store and harmless everywhere else.
function Get-BlobAccount {
    $cs = Get-PiuSecret 'AzureBlob:ConnectionSTring'
    if (-not $cs) { $cs = Get-PiuSecret 'AzureBlob:ConnectionString' }
    if (-not $cs) { throw "no AzureBlob connection string in the user-secrets store - is the AppHost configured?" }
    $parts = @{}
    foreach ($kv in ($cs -split ';')) {
        if (-not $kv) { continue }
        $i = $kv.IndexOf('=')
        if ($i -lt 1) { continue }
        $parts[$kv.Substring(0, $i)] = $kv.Substring($i + 1)
    }
    if (-not $parts.ContainsKey('AccountName') -or -not $parts.ContainsKey('AccountKey')) {
        throw 'connection string has no AccountName/AccountKey'
    }
    return [pscustomobject]@{ Name = $parts['AccountName']; Key = $parts['AccountKey'] }
}

# Service SAS over one container, signed locally. Permission letters MUST be given in the
# service's canonical order (racwdxltmeop) or the signature verifies against a different
# string than the one the service rebuilds, and every request 403s.
function New-ContainerSas {
    param(
        [Parameter(Mandatory)]$Account,
        [string]$Container = '$web',
        [Parameter(Mandatory)][string]$Permissions,
        [int]$Hours = 6
    )
    $sv = '2022-11-02'
    $st = (Get-Date).ToUniversalTime().AddMinutes(-5).ToString('yyyy-MM-ddTHH:mm:ssZ')
    $se = (Get-Date).ToUniversalTime().AddHours($Hours).ToString('yyyy-MM-ddTHH:mm:ssZ')
    $sr = 'c'
    $spr = 'https'
    $canonical = "/blob/$($Account.Name)/$Container"
    # 16 fields, in this order, for sv >= 2020-12-06: sp st se canonical si sip spr sv sr
    # snapshot ses rscc rscd rsce rscl rsct
    $toSign = @($Permissions, $st, $se, $canonical, '', '', $spr, $sv, $sr, '', '', '', '', '', '', '') -join "`n"
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    try {
        $hmac.Key = [Convert]::FromBase64String($Account.Key)
        $sig = [Convert]::ToBase64String($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($toSign)))
    } finally { $hmac.Dispose() }
    return "sv=$sv&st=$([uri]::EscapeDataString($st))&se=$([uri]::EscapeDataString($se))&sr=$sr&sp=$Permissions&spr=$spr&sig=$([uri]::EscapeDataString($sig))"
}

function Get-ContainerBaseUrl {
    param([Parameter(Mandatory)]$Account, [string]$Container = '$web')
    # '$web' must ride the URL percent-encoded; a bare $ is legal but some proxies eat it.
    return "https://$($Account.Name).blob.core.windows.net/$([uri]::EscapeDataString($Container))"
}

function ConvertTo-BlobPath {
    param([Parameter(Mandatory)][string]$Path)
    return (($Path.TrimStart('/') -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/')
}

$script:TypeByExtension = @{
    'png' = 'image/png'; 'jpg' = 'image/jpeg'; 'jpeg' = 'image/jpeg'
    'gif' = 'image/gif'; 'webp' = 'image/webp'; 'svg' = 'image/svg+xml'; 'ico' = 'image/x-icon'
}

# Reads the format out of the file's own magic bytes. A download that answered with an error
# page, a login redirect or a truncated body still lands on disk as a file of the right name,
# and uploading one of those would replace real art with garbage that only shows up as a
# broken image on the site days later. The bytes have to say what the extension claims.
function Get-ImageFormat {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    if ($Bytes.Length -lt 12) { return $null }
    $hex = ($Bytes[0..7] | ForEach-Object { $_.ToString('x2') }) -join ''
    if ($hex -eq '89504e470d0a1a0a') { return 'png' }
    if ($hex.StartsWith('ffd8ff')) { return 'jpg' }
    if ($hex.StartsWith('47494638')) { return 'gif' }
    $riff = [Text.Encoding]::ASCII.GetString($Bytes[0..3])
    $webp = [Text.Encoding]::ASCII.GetString($Bytes[8..11])
    if ($riff -eq 'RIFF' -and $webp -eq 'WEBP') { return 'webp' }
    return $null
}

function Get-ContentTypeForPath {
    param([Parameter(Mandatory)][string]$Path)
    if ($Path -notmatch '\.([A-Za-z0-9]+)$') { return $null }
    $ext = $Matches[1].ToLowerInvariant()
    if (-not $script:TypeByExtension.ContainsKey($ext)) { return $null }
    return $script:TypeByExtension[$ext]
}

# Lists every blob under an optional prefix. Goes through the REST API rather than `az`
# because az's piped output ASCII-strips unicode blob names (Korean qualifier folders,
# country flags), and PS 5.1 decodes charset-less XML as Latin-1 - so the bytes are decoded
# as UTF-8 by hand here.
function Get-BlobList {
    param([Parameter(Mandatory)]$Account, [Parameter(Mandatory)][string]$Sas, [string]$Prefix = '', [string]$Container = '$web')
    $base = Get-ContainerBaseUrl -Account $Account -Container $Container
    $blobs = @()
    $marker = ''
    do {
        $url = "${base}?restype=container&comp=list&maxresults=5000&$Sas"
        if ($Prefix) { $url += "&prefix=$([uri]::EscapeDataString($Prefix))" }
        if ($marker) { $url += "&marker=$([uri]::EscapeDataString($marker))" }
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing
        $text = [Text.Encoding]::UTF8.GetString($resp.RawContentStream.ToArray())
        [xml]$page = $text.Substring($text.IndexOf('<'))
        $blobs += @($page.EnumerationResults.Blobs.Blob | Where-Object { $_ })
        $marker = $page.EnumerationResults.NextMarker
    } while ($marker)
    return $blobs
}

Export-ModuleMember -Function Get-PiuSecret, Get-BlobAccount, New-ContainerSas, Get-ContainerBaseUrl,
    ConvertTo-BlobPath, Get-ContentTypeForPath, Get-ImageFormat, Get-BlobList
