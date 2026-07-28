# PostgreSQL Backup And Restore Runbook

This runbook is the MVP baseline for AFK4 PostgreSQL backup and restore
rehearsal. It applies to the cloud Platform API database, including organizations,
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
- organization export tooling;
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

## Scripted Rehearsal

Prefer the repository script for repeatable release rehearsals. It follows the
manual sequence below, redacts PostgreSQL URLs in console output, refuses to
place database dumps inside the repository, generates the EF migration script
under ignored `artifacts/`, restores into the rehearsal database, applies
pending migrations, and samples important table counts.

Run a dry run first to verify command planning without touching PostgreSQL:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/rehearse-postgres-restore.ps1 `
  -DryRun
```

Then run the rehearsal with secret values supplied through environment
variables or a secret manager:

```powershell
$env:AFK4_BACKUP_SOURCE_URL = '<source PostgreSQL URL from secret manager>'
$env:AFK4_RESTORE_TARGET_URL = '<empty rehearsal PostgreSQL URL from secret manager>'

powershell -ExecutionPolicy Bypass -File scripts/rehearse-postgres-restore.ps1 `
  -BackupRoot 'D:\afk4-backups'
```

If the PostgreSQL client source/target values use libpq URLs but the EF
migration step needs an Npgsql connection string, pass the EF target
explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/rehearse-postgres-restore.ps1 `
  -RestoreTargetUrl $env:AFK4_RESTORE_TARGET_URL `
  -EfRestoreTargetConnectionString 'Host=<host>;Port=<port>;Database=<restore-db>;Username=<user>;Password=<password>;SSL Mode=Disable;GSS Encryption Mode=Disable'
```

If PostgreSQL client tools are not in `PATH`, pass explicit paths:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/rehearse-postgres-restore.ps1 `
  -PgDumpPath 'C:\Program Files\PostgreSQL\17\bin\pg_dump.exe' `
  -PgRestorePath 'C:\Program Files\PostgreSQL\17\bin\pg_restore.exe' `
  -PsqlPath 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
```

If Docker is available but PostgreSQL client tools are not installed on the
release machine, run the PostgreSQL client commands through the official
PostgreSQL image:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/rehearse-postgres-restore.ps1 `
  -PostgresClientMode docker `
  -PostgresDockerImage postgres:17-alpine
```

When the source or restore target is published on the Windows host as
`localhost` or `127.0.0.1`, the script automatically uses
`host.docker.internal` only for the containerized PostgreSQL client commands.
The .NET migration step still uses the original restore target URL from the
host process.

The script does not create the rehearsal database. Create an empty target
database through the provider console, `createdb`, Coolify, or a local
PostgreSQL admin workflow before running the real restore.

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

