# scripts

Operational scripts that aren't part of the app build.

## seed-export — produce a public-only seed `.bak`

Refreshes the seed database that contributors download to populate their local
dev environment with realistic chart + public-player data.

### When to run

- After any schema change that adds tables holding user data (so the new
  schema ships in the `.bak`).
- When the public player data is stale enough that the seed is misleading
  (informally — quarterly is plenty).
- When someone asks for a refreshed dump.

### What it does

1. Restores the prod export as `ScoreTracker_Export` in the dev SQL container.
2. Wipes credential tables (`UserApiToken`, `ExternalLogin`).
3. Deletes non-public users — cascading FKs purge their scores, sessions,
   preferences, etc.
4. Backs up the pruned DB with compression.
5. Copies the resulting `.bak` to your host filesystem.
6. Drops the temp DB and removes the in-container files.

The prune logic is in [`seed-export.sql`](seed-export.sql) — six lines of
SQL plus comments. The wrappers handle the orchestration.

### Prerequisites

- **Docker** running with the dev container up: `docker compose up -d` from
  the repo root.
- **For `.bacpac` sources** (Azure SQL native export): SqlPackage on PATH.
  Install with `dotnet tool install -g microsoft.sqlpackage`.

### Usage

```bash
# Mac / Linux / WSL / Git Bash
./scripts/seed-export.sh prod-export.bacpac
./scripts/seed-export.sh prod-export.bak dist/seed-2026-04-25.bak

# Windows PowerShell
./scripts/seed-export.ps1 prod-export.bacpac
./scripts/seed-export.ps1 prod-export.bak -Output dist\seed-2026-04-25.bak
```

The script auto-detects the source format by file extension:

- `.bacpac` → imported via SqlPackage (typical for Azure SQL exports).
- `.bak` → restored via `RESTORE DATABASE FROM DISK`.

Output defaults to `out/seed-YYYY-MM-DD.bak` (relative to wherever you run
the script from). The output is always a `.bak` regardless of input format,
because the local dev contract uses `RESTORE DATABASE` for the seed.

### Getting the prod export

How you get the `.bacpac` from Azure SQL is out of scope for this script.
The two common paths:

- **Portal** → SQL database → Export → choose a Storage account.
- **CLI** — `az sql db export --resource-group ... --server ... --name ... --storage-key-type StorageAccessKey --storage-key ... --storage-uri ... --admin-user ... --admin-password ...`

Either way, download the resulting `.bacpac` and feed it to `seed-export`.

### Hosting

After the script finishes, upload the produced `.bak` to wherever the
`scripts/dev-up.{sh,ps1}` script (still TODO) will fetch it from.
Recommended hosts in priority order:

1. **GitHub Releases** — free, public, versioned, easy to delete a bad
   dump. Upload as a release asset; reference by URL.
2. **Public Azure Blob container** under the existing storage account —
   already paid for; rev versions via blob naming.
3. **Cloudflare R2 public bucket** — free egress.

### Safety notes

- The script always runs cleanup (drops the temp DB, removes in-container
  files) on exit, even if a step fails. No state should leak between runs.
- The temp DB is named `ScoreTracker_Export` to avoid colliding with your
  local dev `ScoreTracker` DB. Both can coexist on the same SQL instance.
- Running this against your dev container does **not** touch your local
  `ScoreTracker` DB.

### Troubleshooting

**`Container 'scoretracker-db' isn't running.`**
Start the dev stack: `docker compose up -d` from the repo root.

**`SqlPackage not on PATH.`**
`dotnet tool install -g microsoft.sqlpackage`. May require restarting your
shell to pick up the new PATH entry.

**`Msg 1785, Level 16 ... cascade paths.`**
A FK relationship has multi-path cascade and SQL Server refuses to delete.
This means the cascade-delete-on-User assumption broke for some table.
Either (a) inspect the offending table and add an explicit
`DELETE FROM dbo.<Table> WHERE UserId IN (SELECT Id FROM dbo.[User] WHERE
IsPublic = 0)` before the User delete in `seed-export.sql`, or (b) change
the FK to `ON DELETE NO ACTION` and handle that table explicitly. The
script will fail loudly so you'll see exactly which table.

**`docker cp` errors / "no such file"**
Almost always means a previous run left the container in a weird state.
Re-run — the script's `Cleanup` runs at start and end.
