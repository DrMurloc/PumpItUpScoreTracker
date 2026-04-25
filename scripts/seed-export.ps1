<#
.SYNOPSIS
Produce a public-only seed .bak from a prod export.

.DESCRIPTION
Auto-detects whether the source is .bacpac (Azure SQL native export) or
.bak (SQL Server backup). Restores it as a temp DB in the dev SQL container,
runs scripts/seed-export.sql to drop non-public users and credential tables,
backs up the pruned DB, copies the .bak out, and cleans up.

Requires the dev container to be running (docker compose up -d). For .bacpac
sources, also requires SqlPackage on PATH (install via:
  dotnet tool install -g microsoft.sqlpackage).

.EXAMPLE
./scripts/seed-export.ps1 prod.bacpac
./scripts/seed-export.ps1 prod.bak -Output dist/seed-2026-04-25.bak
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Source,

    [string]$Output = "out/seed-$(Get-Date -Format 'yyyy-MM-dd').bak"
)

$ErrorActionPreference = 'Stop'

# Constants — match docker-compose.yml at the repo root.
$Container           = 'scoretracker-db'
$SaPassword          = 'Your_password123'
$SqlHost             = 'localhost,1433'
$ExportDb            = 'ScoreTracker_Export'
$ContainerBackupDir  = '/var/opt/mssql/backups'
$ContainerSourceBak  = "$ContainerBackupDir/source.bak"
$ContainerOutputBak  = "$ContainerBackupDir/output.bak"
$ScriptDir           = $PSScriptRoot
$PruneSqlPath        = Join-Path $ScriptDir 'seed-export.sql'

function Invoke-SqlcmdInContainer {
    param(
        [Parameter(Mandatory = $true)] [string]$Query,
        [string]$Database = 'master'
    )
    & docker exec $Container /opt/mssql-tools18/bin/sqlcmd `
        -S localhost -U sa -P $SaPassword -No `
        -d $Database -Q $Query
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed (exit $LASTEXITCODE)" }
}

function Drop-ExportDb {
    try {
        Invoke-SqlcmdInContainer -Query @"
IF DB_ID('$ExportDb') IS NOT NULL BEGIN
    ALTER DATABASE [$ExportDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$ExportDb];
END
"@
    }
    catch {
        Write-Warning "Could not drop $ExportDb (may not exist yet): $_"
    }
}

function Cleanup {
    Write-Host '==> Cleaning up...' -ForegroundColor Yellow
    Drop-ExportDb
    docker exec $Container rm -f $ContainerSourceBak $ContainerOutputBak 2>&1 | Out-Null
}

# ---------- Pre-flight ----------

if (-not (Test-Path $Source))       { throw "Source not found: $Source" }
if (-not (Test-Path $PruneSqlPath)) { throw "Prune script not found: $PruneSqlPath" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is not installed or not on PATH.'
}

$containerStatus = docker inspect -f '{{.State.Status}}' $Container 2>$null
if ($LASTEXITCODE -ne 0 -or $containerStatus -ne 'running') {
    throw "Container '$Container' isn't running. Start it: docker compose up -d"
}

$ext = [IO.Path]::GetExtension($Source).ToLowerInvariant()
if ($ext -notin @('.bacpac', '.bak')) {
    throw "Unsupported source extension: $ext (expected .bacpac or .bak)"
}

if ($ext -eq '.bacpac' -and -not (Get-Command SqlPackage -ErrorAction SilentlyContinue)) {
    throw 'SqlPackage not on PATH. Install: dotnet tool install -g microsoft.sqlpackage'
}

$outDir = Split-Path -Parent $Output
if ($outDir -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

# ---------- Run ----------

try {
    Cleanup  # In case a previous run left state behind.

    docker exec $Container mkdir -p $ContainerBackupDir | Out-Null

    if ($ext -eq '.bacpac') {
        Write-Host "==> Importing $Source via SqlPackage (this can take a few minutes)..." -ForegroundColor Cyan
        & SqlPackage `
            /Action:Import `
            "/SourceFile:$Source" `
            "/TargetServerName:$SqlHost" `
            '/TargetUser:sa' `
            "/TargetPassword:$SaPassword" `
            "/TargetDatabaseName:$ExportDb" `
            '/TargetTrustServerCertificate:True'
        if ($LASTEXITCODE -ne 0) { throw "SqlPackage Import failed (exit $LASTEXITCODE)" }
    }
    else {
        Write-Host "==> Copying $Source into container..." -ForegroundColor Cyan
        & docker cp $Source "${Container}:${ContainerSourceBak}"
        if ($LASTEXITCODE -ne 0) { throw 'docker cp failed' }

        Write-Host '==> Restoring as temp DB (this can take a few minutes)...' -ForegroundColor Cyan
        Invoke-SqlcmdInContainer -Query @"
SET NOCOUNT ON;
DECLARE @sql NVARCHAR(MAX);
DECLARE @data NVARCHAR(128), @log NVARCHAR(128);
CREATE TABLE #Files (
    LogicalName NVARCHAR(128), PhysicalName NVARCHAR(260), [Type] CHAR(1),
    FileGroupName NVARCHAR(128), Size NUMERIC(20,0), MaxSize NUMERIC(20,0),
    FileId BIGINT, CreateLSN NUMERIC(25,0), DropLSN NUMERIC(25,0),
    UniqueId UNIQUEIDENTIFIER, ReadOnlyLSN NUMERIC(25,0), ReadWriteLSN NUMERIC(25,0),
    BackupSizeInBytes BIGINT, SourceBlockSize INT, FileGroupId INT,
    LogGroupGUID UNIQUEIDENTIFIER, DifferentialBaseLSN NUMERIC(25,0),
    DifferentialBaseGUID UNIQUEIDENTIFIER, IsReadOnly BIT, IsPresent BIT,
    TDEThumbprint VARBINARY(32), SnapshotUrl NVARCHAR(360)
);
INSERT INTO #Files EXEC('RESTORE FILELISTONLY FROM DISK = ''$ContainerSourceBak''');
SELECT TOP 1 @data = LogicalName FROM #Files WHERE [Type] = 'D';
SELECT TOP 1 @log  = LogicalName FROM #Files WHERE [Type] = 'L';
SET @sql = N'RESTORE DATABASE [$ExportDb] FROM DISK = ''$ContainerSourceBak''
WITH MOVE ''' + @data + ''' TO ''/var/opt/mssql/data/${ExportDb}.mdf'',
     MOVE ''' + @log  + ''' TO ''/var/opt/mssql/data/${ExportDb}_log.ldf'',
     REPLACE, STATS = 10';
EXEC sp_executesql @sql;
"@
    }

    Write-Host '==> Pruning to public-only data...' -ForegroundColor Cyan
    $pruneSql = Get-Content -Raw $PruneSqlPath
    Invoke-SqlcmdInContainer -Query $pruneSql -Database $ExportDb

    Write-Host '==> Backing up pruned database...' -ForegroundColor Cyan
    Invoke-SqlcmdInContainer -Query @"
BACKUP DATABASE [$ExportDb]
TO DISK = '$ContainerOutputBak'
WITH FORMAT, INIT, COMPRESSION, STATS = 10
"@

    Write-Host '==> Copying backup out of container...' -ForegroundColor Cyan
    & docker cp "${Container}:${ContainerOutputBak}" $Output
    if ($LASTEXITCODE -ne 0) { throw 'docker cp failed' }

    $sizeMb = [math]::Round((Get-Item $Output).Length / 1MB, 1)
    Write-Host ''
    Write-Host "Done. Wrote $Output ($sizeMb MB)" -ForegroundColor Green
    Write-Host '  Upload it to whatever public hosting (GitHub Releases, public Blob, etc.).'
}
finally {
    Cleanup
}
