#!/usr/bin/env bash
# Produce a public-only seed .bak from a prod export.
#
# Auto-detects whether the source is .bacpac (Azure SQL native export) or
# .bak (SQL Server backup). Restores it as a temp DB in the dev SQL container,
# runs scripts/seed-export.sql to drop non-public users and credential tables,
# backs up the pruned DB, copies the .bak out, and cleans up.
#
# Requires the dev container to be running (docker compose up -d). For .bacpac
# sources, also requires SqlPackage on PATH (install via:
#   dotnet tool install -g microsoft.sqlpackage).
#
# Usage:
#   ./scripts/seed-export.sh prod.bacpac
#   ./scripts/seed-export.sh prod.bak dist/seed-2026-04-25.bak

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PRUNE_SQL="$SCRIPT_DIR/seed-export.sql"

# Constants — match docker-compose.yml at the repo root.
CONTAINER='scoretracker-db'
SA_PASSWORD='Your_password123'
SQL_HOST='localhost,1433'
EXPORT_DB='ScoreTracker_Export'
CONTAINER_BACKUP_DIR='/var/opt/mssql/backups'
CONTAINER_SOURCE_BAK="$CONTAINER_BACKUP_DIR/source.bak"
CONTAINER_OUTPUT_BAK="$CONTAINER_BACKUP_DIR/output.bak"

usage() {
    cat <<EOF >&2
Usage: $0 <source.bacpac|source.bak> [output.bak]

Produces a public-only seed .bak from a prod export. Defaults output to
out/seed-YYYY-MM-DD.bak.

Requirements:
  - Dev container running: docker compose up -d
  - For .bacpac sources: SqlPackage on PATH
    (dotnet tool install -g microsoft.sqlpackage)
EOF
    exit 1
}

[ $# -ge 1 ] || usage
SOURCE="$1"
OUTPUT="${2:-out/seed-$(date +%Y-%m-%d).bak}"

sqlcmd_in_container() {
    local query="$1"
    local db="${2:-master}"
    docker exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd \
        -S localhost -U sa -P "$SA_PASSWORD" -No \
        -d "$db" -Q "$query"
}

drop_export_db() {
    sqlcmd_in_container "
IF DB_ID('$EXPORT_DB') IS NOT NULL BEGIN
    ALTER DATABASE [$EXPORT_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$EXPORT_DB];
END" 2>/dev/null || true
}

cleanup() {
    echo "==> Cleaning up..."
    drop_export_db
    docker exec "$CONTAINER" rm -f "$CONTAINER_SOURCE_BAK" "$CONTAINER_OUTPUT_BAK" 2>/dev/null || true
}

# ---------- Pre-flight ----------

[ -f "$SOURCE" ]    || { echo "Source not found: $SOURCE" >&2; exit 1; }
[ -f "$PRUNE_SQL" ] || { echo "Prune script not found: $PRUNE_SQL" >&2; exit 1; }

command -v docker >/dev/null 2>&1 || { echo "Docker is not installed or not on PATH." >&2; exit 1; }

if [ "$(docker inspect -f '{{.State.Status}}' "$CONTAINER" 2>/dev/null || echo missing)" != 'running' ]; then
    echo "Container '$CONTAINER' isn't running. Start it: docker compose up -d" >&2
    exit 1
fi

ext="${SOURCE##*.}"
ext="$(echo "$ext" | tr '[:upper:]' '[:lower:]')"
case "$ext" in
    bacpac|bak) ;;
    *) echo "Unsupported source extension: .$ext (expected .bacpac or .bak)" >&2; exit 1 ;;
esac

if [ "$ext" = 'bacpac' ] && ! command -v SqlPackage >/dev/null 2>&1 && ! command -v sqlpackage >/dev/null 2>&1; then
    echo "SqlPackage not on PATH. Install: dotnet tool install -g microsoft.sqlpackage" >&2
    exit 1
fi

mkdir -p "$(dirname "$OUTPUT")"

# Always cleanup on exit
trap cleanup EXIT

cleanup  # In case a previous run left state behind.

docker exec "$CONTAINER" mkdir -p "$CONTAINER_BACKUP_DIR" >/dev/null

# ---------- Restore (path depends on format) ----------

if [ "$ext" = 'bacpac' ]; then
    echo "==> Importing $SOURCE via SqlPackage (this can take a few minutes)..."
    SQLPKG="$(command -v SqlPackage 2>/dev/null || command -v sqlpackage)"
    "$SQLPKG" \
        /Action:Import \
        "/SourceFile:$SOURCE" \
        "/TargetServerName:$SQL_HOST" \
        /TargetUser:sa \
        "/TargetPassword:$SA_PASSWORD" \
        "/TargetDatabaseName:$EXPORT_DB" \
        /TargetTrustServerCertificate:True
else
    echo "==> Copying $SOURCE into container..."
    docker cp "$SOURCE" "${CONTAINER}:${CONTAINER_SOURCE_BAK}"

    echo "==> Restoring as temp DB (this can take a few minutes)..."
    sqlcmd_in_container "
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
INSERT INTO #Files EXEC('RESTORE FILELISTONLY FROM DISK = ''$CONTAINER_SOURCE_BAK''');
SELECT TOP 1 @data = LogicalName FROM #Files WHERE [Type] = 'D';
SELECT TOP 1 @log  = LogicalName FROM #Files WHERE [Type] = 'L';
SET @sql = N'RESTORE DATABASE [$EXPORT_DB] FROM DISK = ''$CONTAINER_SOURCE_BAK''
WITH MOVE ''' + @data + ''' TO ''/var/opt/mssql/data/${EXPORT_DB}.mdf'',
     MOVE ''' + @log  + ''' TO ''/var/opt/mssql/data/${EXPORT_DB}_log.ldf'',
     REPLACE, STATS = 10';
EXEC sp_executesql @sql;"
fi

# ---------- Prune + backup + extract ----------

echo "==> Pruning to public-only data..."
sqlcmd_in_container "$(cat "$PRUNE_SQL")" "$EXPORT_DB"

echo "==> Backing up pruned database..."
sqlcmd_in_container "
BACKUP DATABASE [$EXPORT_DB]
TO DISK = '$CONTAINER_OUTPUT_BAK'
WITH FORMAT, INIT, COMPRESSION, STATS = 10"

echo "==> Copying backup out of container..."
docker cp "${CONTAINER}:${CONTAINER_OUTPUT_BAK}" "$OUTPUT"

SIZE="$(du -h "$OUTPUT" | cut -f1)"
echo ""
echo "Done. Wrote $OUTPUT ($SIZE)"
echo "  Upload it to whatever public hosting (GitHub Releases, public Blob, etc.)."
