# Coolify Staging Deploy Runbook

This runbook creates the first AFK4 staging backend on a Linux VPS managed by
Coolify. It keeps the MVP runtime boundaries intact: the cloud Platform API and
server-side dependencies are containerized first. The internal Platform Control
is an approved platform-owner surface, but its frontend deployment is a
follow-up until the Platform Control implementation exists. Organization Admin,
Agent Service, and Player Shell remain Windows client runtimes and are not
deployed as Linux services.

Do not commit secrets, filled environment files, database dumps, generated
migration SQL, certificates, signing keys, or Coolify tokens.

## Scope

Included:

- Platform API container built by Coolify from this repository;
- PostgreSQL as the staging source of truth;
- externalized environment variables and secrets;
- controlled EF migration procedure;
- health and smoke verification;
- basic rollback and diagnostics.

Not included:

- local club server;
- Platform Control frontend deployment before the Platform Control slice is
  implemented;
- customer browser operational admin as the primary club UI;
- Linux services for Organization Admin, Agent Service, or Player Shell;
- production-grade backup retention, PITR, or monitoring automation.

## Repository Inputs

Use these Coolify settings for the Platform API app:

| Setting | Value |
| --- | --- |
| Build context | repository root |
| Dockerfile path | `src/AFK4.Platform.Api/Dockerfile` |
| Exposed port | `8080` |
| Health path | `/api/health` |
| Environment template | `deploy/coolify/staging.env.template` |

The Dockerfile publishes only `src/AFK4.Platform.Api` and its shared project
references. The app reads the PostgreSQL connection from
`ConnectionStrings__PlatformDatabase`. The runtime image includes `curl` and
`wget` so Coolify can execute Dockerfile-based container health checks inside
the container.

## Create PostgreSQL

Preferred: create a Coolify-managed PostgreSQL service in the same project or
environment as the Platform API.

1. In Coolify, add a new PostgreSQL resource.
2. Use a staging-only database name such as `afk4_staging`.
3. Let Coolify generate the database password or store your generated password
   in Coolify secrets.
4. Keep PostgreSQL reachable only through the Coolify/internal network unless a
   temporary migration tunnel is explicitly needed.
5. Copy the internal host, database, username, and password into the Platform
   API app environment variable `ConnectionStrings__PlatformDatabase`.

Fallback: if the managed PostgreSQL service path is unavailable, create a
separate Coolify compose/service resource from
`deploy/coolify/staging-postgres.fallback.compose.yaml`.

- Set `AFK4_STAGING_POSTGRES_PASSWORD` in Coolify secrets.
- Override `AFK4_STAGING_POSTGRES_DB` and `AFK4_STAGING_POSTGRES_USER` only if
  the defaults do not match the staging convention.
- Do not expose PostgreSQL with public `ports`.
- Do not use the local development `POSTGRES_HOST_AUTH_METHOD=trust` pattern in
  staging.

## Configure The Platform API App

1. Create a new Coolify application from the GitHub repository.
2. Select Dockerfile-based build.
3. Set **Build context** to the repository root.
4. Set **Dockerfile path** to `src/AFK4.Platform.Api/Dockerfile`.
5. Set the internal application port to `8080`.
6. Configure the public staging domain and TLS in Coolify.
7. Copy variable names from `deploy/coolify/staging.env.template` into Coolify.
8. Fill values in Coolify only; do not create a filled env file in the repo.
9. Mark application variables as runtime-only in Coolify. Do not expose
   `ConnectionStrings__PlatformDatabase` or `Sessions__SigningPrivateKeyPem`
   as build-time variables.
10. Include `localhost` and `127.0.0.1` in `AllowedHosts` so Coolify's
    in-container health check can call `http://localhost:8080/api/health`.

Required Platform API variables:

```text
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://+:8080
AllowedHosts=<coolify-staging-domain>;localhost;127.0.0.1
Install__ApiBaseUrl=https://<coolify-staging-domain>
Install__UpdateChannel=internal
Install__UpdatePackageSigningPublicKeyPem=<public PEM from deploy/coolify/staging-update-signing-public.pem>
Cors__PlatformWebOrigins__0=https://platform.afk4.net
ConnectionStrings__PlatformDatabase=<Coolify PostgreSQL connection string>
Sessions__SigningPrivateKeyPem=<Coolify secret PEM>
SupportAccess__OrganizationAdminBaseUrl=<Organization Admin base URL for this environment>
```

`SupportAccess__OrganizationAdminBaseUrl` is required in every environment; the
API builds the support-mode sign-in link from it. The `appsettings.json`
default is empty, and an empty value makes the API build a relative link that
opens inside the Platform Control origin instead of Organization Admin, so
support sign-in silently fails. See `docs/runbooks/support-mode.md` for
details.

The `Install__*` entries are returned by `/api/install/enroll` and then written
by the Setup Wizard into Agent machine environment variables. If
`Install__ApiBaseUrl` is missing, the API falls back to the local development
default `http://localhost:5074`, causing clean staging VMs to enroll
successfully but then send Agent heartbeat/update traffic to localhost. Use the
contents of `deploy/coolify/staging-update-signing-public.pem` for
`Install__UpdatePackageSigningPublicKeyPem`; it is a public verification key,
but it must be preserved as multiline text.

The `Cors__PlatformWebOrigins__*` entry allows the browser-hosted Platform Control
to call the Platform API. Do not keep the retired `app.*` origin after the
external club application is decommissioned.

For Coolify internal PostgreSQL, include `SSL Mode=Disable` unless SSL has been
explicitly configured for that database. For Linux containers, include
`GSS Encryption Mode=Disable` in the Npgsql connection string unless
Kerberos/GSS encryption is intentionally configured. Npgsql can otherwise log a
harmless fallback message about `libgssapi_krb5.so.2` being unavailable in
minimal runtime images.

Generate the session lease key outside the repository. Example with Git for
Windows OpenSSL on a trusted release workstation:

```powershell
$privateKeyPath = Join-Path $env:TEMP 'afk4-staging-session-lease-private.pem'
$publicKeyPath = Join-Path $env:TEMP 'afk4-staging-session-lease-public.pem'
& 'C:\Program Files\Git\usr\bin\openssl.exe' ecparam -name prime256v1 -genkey -noout -out $privateKeyPath
& 'C:\Program Files\Git\usr\bin\openssl.exe' ec -in $privateKeyPath -pubout -out $publicKeyPath
```

Paste the private PEM into Coolify as `Sessions__SigningPrivateKeyPem`. The
matching public PEM is not secret and should be committed at
`deploy/coolify/staging-session-signing-public.pem` so every release
workstation can build the staging Gaming PC setup executable with the same
Agent lease verification key. When rotating the staging private key in Coolify,
replace this public PEM in the same change.

## EF Migration Order

Do not run `Database.Migrate()` automatically from the web container on startup.
Apply EF migrations as an explicit release step so schema changes are visible,
repeatable, and recoverable.

For staging:

1. Confirm the target database is staging, not production.
2. Take a Coolify/PostgreSQL snapshot or logical backup before schema changes.
3. Restore .NET tools locally:

   ```powershell
   & 'C:\Program Files\dotnet\dotnet.exe' tool restore
   ```

4. Generate an idempotent SQL script for review. The output path is ignored by
   git:

   ```powershell
   New-Item -ItemType Directory -Force -Path artifacts/ef-migrations | Out-Null
   & 'C:\Program Files\dotnet\dotnet.exe' ef migrations script `
     --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
     --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
     --idempotent `
     --output artifacts/ef-migrations/afk4-platform-staging-idempotent.sql
   ```

5. Apply migrations once from a trusted release workstation or a controlled
   Coolify one-off shell that has network access to the staging database:

   ```powershell
   $env:ConnectionStrings__PlatformDatabase = $env:AFK4_STAGING_DATABASE_URL
   & 'C:\Program Files\dotnet\dotnet.exe' ef database update `
     --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
     --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
   ```

   In shells where `dotnet` is already on `PATH`, this is the same migration
   operation as `dotnet ef database update`.

6. Record the applied commit, migration command, and smoke evidence in the
   progress snapshot when this is run for real.

Миграция `RebaseBillingCurrencyToTjs` меняет код валюты существующих тарифов,
подписок и счетов с `RUB` на `TJS` **без пересчёта сумм**: 2 900 рублей становятся
2 900 сомони. Для staging это осознанное решение — продакшн ещё не развёрнут.
Никакой конвертации по курсу не происходит, и читать её как конвертацию нельзя.

If staging PostgreSQL is not externally reachable, use Coolify's terminal,
private network tooling, or a temporary SSH tunnel approved for the VPS. Close
temporary access immediately after migrations and smoke checks.

## Deploy

Normal staging deploys are automated by the GitHub Actions workflow
`.github/workflows/coolify-staging-deploy.yml`.

Configure these GitHub repository variables once:

```text
COOLIFY_BASE_URL=https://<coolify-instance-domain>
COOLIFY_STAGING_APP_UUID=<Coolify Platform API app UUID>
AFK4_STAGING_PLATFORM_BASE_URL=https://<coolify-staging-domain>
```

Configure this GitHub repository secret once:

```text
COOLIFY_API_TOKEN=<Coolify API token>
```

The token must be a Coolify bearer token created under `Keys & Tokens` / `API
tokens`. Do not put the token in repository variables, workflow inputs, logs,
or committed files.

The workflow runs on `main` pushes that touch the Platform API, shared backend
contracts/build inputs, Coolify deployment files, or the workflow itself. It
can also be started with `workflow_dispatch`; use `force_rebuild=true` only
when a cache-free Coolify rebuild is needed.

For normal app-only changes:

1. Push the reviewed branch to GitHub and open a PR.
2. Let the PR run `PR Verification Result` on the current head commit.
3. Merge only after the required PR gate is green.
4. The `Coolify Staging Deploy` workflow queues a Coolify deployment through
   `GET /api/v1/deploy?uuid=...&force=...`.
5. The workflow polls `GET /api/v1/deployments/{uuid}` until Coolify reports a
   terminal status.
6. The workflow confirms `AFK4_STAGING_PLATFORM_BASE_URL/api/health` returns
   `status = ok`.

If a pushed commit includes files under
`src/AFK4.Platform.Api/Data/Migrations/`, the automatic deploy fails closed.
First run the EF migration order above, including backup/snapshot and staging
database update. Then start `Coolify Staging Deploy` manually with
`confirm_migrations_applied=true` after the migration has been handled. This
keeps schema changes explicit while removing the manual Coolify click path for
ordinary backend deploys.

## Smoke Verification

Set the public URL in a local PowerShell session:

```powershell
$env:AFK4_STAGING_BASE_URL = 'https://<coolify-staging-domain>'
```

Verify liveness:

```powershell
Invoke-RestMethod "$env:AFK4_STAGING_BASE_URL/api/health"
```

Expected:

```text
status serverTimeUtc
------ -------------
ok     <current UTC timestamp>
```

Verify migration state from a trusted machine with PostgreSQL client access:

```powershell
psql $env:AFK4_STAGING_DATABASE_URL -c 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";'
```

For a deeper staging smoke, adapt the authenticated flow from
`docs/operations/local-postgres-smoke.md` using staging-only organization,
branch, staff, and device data. Do not reuse local dev passwords or trust-auth
database settings.

Minimum first-deploy evidence:

- `/api/health` returns `status = ok`;
- `__EFMigrationsHistory` contains the repository's current migrations;
- the API container logs do not show database connection failures;
- Coolify reports the deployment healthy on the current commit.

## Platform Control + Public-Endpoint Rate-Limiting

`src/AFK4.PlatformControl.Web` is the admin-only Platform Control deployed to the existing
internal platform host. It uses `deploy/coolify/platform-control.Dockerfile` (build
context = repository root, exposed port `8080`, health path `/healthz`):

| Coolify app | Host | Build arguments |
| --- | --- | --- |
| Admin SPA | `platform.api.afk4.net` | `VITE_PLATFORM_API_BASE_URL=https://api.afk4.net` |

The host exposes `/admin/*`. Verify `/admin` in a browser and confirm that old
`/club/install` and browser staff sign-in/reset URLs render the explicit
not-found state. `/account-activation` remains the public Organization Owner
activation route issued by Platform Control.
The old `app.*` Coolify application and DNS entry must be decommissioned only
through a separate explicitly approved external operation.

Wire both dedicated hosts via the Traefik labels in
[`deploy/coolify/ingress.md`](../../deploy/coolify/ingress.md#1-platformweb-control-plane).

Same file documents how to attach a per-source-IP rate-limit middleware to the
public Platform API endpoints that accept bearer-style setup, install, or
connection credentials with no auth handshake in front of them
(`/api/operator-connections/resolve`,
`/api/account-activation/organization-owner`,
`/api/organizations/{organizationId}/install/discover`, and
`/api/organizations/{organizationId}/install/enroll`). The labels go on the
existing Platform API Coolify application and create higher-priority Traefik
routers for those exact paths, so the rest of the API surface stays unchanged.
Verify after deploying using the curl recipes at the bottom of the ingress doc.

## Rollback

Rollback the app container through Coolify by redeploying the previous
successful deployment or previous known-good commit.

Database rollback is separate:

- do not assume app rollback reverses EF migrations;
- prefer forward-compatible migrations for staging and production;
- if a migration corrupts staging data, restore the pre-migration backup only
  after confirming the target database and preserving failure evidence;
- record the restore command and post-restore smoke result in
  `docs/progress/2026-05-12-vertical-slice-progress.md` when the rehearsal is
  real project state.

## Diagnostics

When deploy or smoke fails:

1. Check Coolify build logs for restore/publish failures.
2. Check runtime logs for unhandled exceptions, missing
   `ConnectionStrings__PlatformDatabase`, or invalid
   `Sessions__SigningPrivateKeyPem`.
3. Confirm the app listens on port `8080`.
4. Confirm PostgreSQL health in Coolify.
5. Confirm the connection string host is the Coolify/internal host, not
   `localhost`.
6. Run the EF migration command again; it should be idempotent when no pending
   migrations remain.
7. Query `__EFMigrationsHistory` to confirm schema state.

Common failure signals:

- `Host=localhost` in the app connection string means the API container is
  trying to connect to itself, not PostgreSQL.
- Missing or malformed `Sessions__SigningPrivateKeyPem` causes session lease
  operations to fail.
- Public PostgreSQL ports are unnecessary for normal Coolify operation and
  should be closed after any approved migration tunnel is removed.
