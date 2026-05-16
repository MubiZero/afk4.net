# PostgreSQL Backup And Restore Runbook

This runbook is the MVP baseline for AFK4 PostgreSQL backup and restore
rehearsal. It applies to the cloud Platform API database, including tenants,
branches, devices, sessions, ledger entries, POS, inventory, receipts, updates,
and audit records.

Do not commit database dumps, production connection strings, credentials,
certificates, signing keys, or restore logs containing secrets.

## Scope

Use this runbook before production rollout and before destructive operational
maintenance.

Covered here:

- custom-format logical backups with `pg_dump`;
- restore rehearsal into a new database with `pg_restore`;
- EF migration script generation and staging rehearsal;
- post-restore smoke checks;
- retention and encryption expectations.

Not covered here:

- provider-specific managed backup APIs;
- point-in-time recovery automation;
- tenant export tooling;
- direct destructive data repair scripts.

## Required Tools

Install PostgreSQL client tools on the operator or release machine:

```powershell
pg_dump --version
pg_restore --version
psql --version
```

Restore .NET tools from the repository root:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
```

## Environment

Keep connection strings in the shell environment or a secret manager. Do not
write production secrets into scripts or docs.

Example local variables:

```powershell
$env:AFK4_BACKUP_SOURCE_URL = 'postgresql://postgres@localhost:5432/afk4_dev'
$env:AFK4_RESTORE_TARGET_URL = 'postgresql://postgres@localhost:5432/afk4_restore_rehearsal'
$backupRoot = 'D:\afk4-backups'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = Join-Path $backupRoot "afk4-platform-$timestamp.dump"
```

Create the backup directory outside the repository:

```powershell
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
```

## Create A Backup

Run `pg_dump` in custom format:

```powershell
pg_dump `
  --format=custom `
  --no-owner `
  --no-privileges `
  --file $backupPath `
  $env:AFK4_BACKUP_SOURCE_URL
```

Verify the archive is readable:

```powershell
pg_restore --list $backupPath | Select-Object -First 20
Get-Item $backupPath | Select-Object FullName,Length,LastWriteTime
```

Production expectations:

- encrypt backups at rest;
- store backups outside the application host;
- restrict restore permission to trusted operators;
- keep a retention policy approved by the business owner;
- test restore at least before every production release that changes schema or
  financial/session behavior.

## Generate Migration Script

Generate the current EF migration script for staging review:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations script `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --idempotent `
  --output artifacts/ef-migrations/afk4-platform-idempotent.sql
```

The generated script belongs under ignored `artifacts/`. Do not commit it
unless the team explicitly decides to version reviewed release SQL.

## Restore Rehearsal

Create an empty rehearsal database. The exact command depends on how the
PostgreSQL server is administered. Local example:

```powershell
createdb afk4_restore_rehearsal
```

Restore into the empty database:

```powershell
pg_restore `
  --clean `
  --if-exists `
  --no-owner `
  --no-privileges `
  --dbname $env:AFK4_RESTORE_TARGET_URL `
  $backupPath
```

If the database already contains data and `--clean` is used, confirm that the
target is a rehearsal database. Never run this restore command against
production.

Apply pending migrations to the restored rehearsal database:

```powershell
$env:ConnectionStrings__PlatformDatabase = $env:AFK4_RESTORE_TARGET_URL
& 'C:\Program Files\dotnet\dotnet.exe' ef database update `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

## Post-Restore Smoke

Start the API against the restored database:

```powershell
$env:ConnectionStrings__PlatformDatabase = $env:AFK4_RESTORE_TARGET_URL
& 'C:\Program Files\dotnet\dotnet.exe' run `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --urls http://localhost:5074
```

In another PowerShell window:

```powershell
Invoke-RestMethod http://localhost:5074/api/health
```

Then run authenticated smoke checks appropriate to the release:

- staff sign-in and refresh;
- branch floor-map read;
- device diagnostics read for a permissioned technician or manager;
- audit search read;
- report read/export for a branch with data;
- update rollout status read if update rows exist.

For data integrity, sample the restored database directly:

```powershell
psql $env:AFK4_RESTORE_TARGET_URL -c "select count(*) from ledger_entries;"
psql $env:AFK4_RESTORE_TARGET_URL -c "select count(*) from audit_records;"
psql $env:AFK4_RESTORE_TARGET_URL -c "select count(*) from devices;"
psql $env:AFK4_RESTORE_TARGET_URL -c "select count(*) from sessions;"
```

Audit and ledger records are append-only. Do not fix accidental destructive
mistakes by mutating historical rows. Represent business corrections with
voids, reversals, refunds, manual corrections, or follow-up audit records.

## Restore Decision Checklist

Before restoring production data:

- identify the exact incident and target restore timestamp or backup file;
- confirm the restore target and environment;
- preserve the current production database if possible before replacing it;
- confirm who approved downtime and who validates recovery;
- document commands, backup file hash, start/end time, and smoke results;
- keep credentials and generated logs in the approved secure location.

## Release Gate

Before production launch, AFK4 must have:

- at least one successful restore rehearsal from a fresh backup;
- a verified migration rehearsal against restored data;
- smoke evidence for health, auth, floor map, diagnostics, audit, reports, and
  update status;
- a retention and encryption policy for backups;
- a named operator responsible for restore execution.

