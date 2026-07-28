# Platform Control And Organization Admin Big-Bang Cutover

This runbook releases the backend, Platform Control, Organization Admin, its
MSI, and the identity migration as one compatibility unit. It is intentionally
not a rolling migration: old human sessions and old Organization Admin builds
must not remain usable after the database migration.

Production execution requires explicit approval for the exact release SHA,
image digest, MSI SHA-256, database snapshot identifier, maintenance window,
forced sign-in, and rollback point. Never print connection strings or tokens.

## Release Record

Before the window, create a release record outside the repository and fill all
fields:

```text
Release Git SHA:
Platform API image digest:
Platform Control image digest:
Organization Admin MSI SHA-256:
Organization Admin version: 0.2.0 or newer
Compatibility epoch: 2
Database snapshot identifier:
Last successful restore rehearsal:
Maintenance start / owner:
Previous release SHA and image digests:
```

Record immutable artifact identities, not mutable tags. Calculate the MSI hash
on the release workstation:

```powershell
git rev-parse HEAD
Get-FileHash .\artifacts\client-packages\afk4-organization-admin-0.2.0-internal.msi -Algorithm SHA256
docker image inspect afk4-platform-control:rc --format '{{index .RepoDigests 0}}'
```

## Preflight Gate

1. Complete a restore rehearsal from a recent production-shaped snapshot by
   following [PostgreSQL Backup And Restore](postgres-backup-restore.md). Record
   the snapshot identifier, restore target, duration, and successful smoke.
2. Verify the release SHA passed the complete backend, web, migration, Docker,
   package, and Windows VM checks. Confirm the WiX `UpgradeCode` is unchanged.
3. Confirm the previous backend and Platform Control images and the previous
   MSI are still available. Confirm the operator who can restore the database
   is present for the whole window.
4. Run the unknown-role query against a read-only production connection. The
   result must be `0`; any other result cancels the cutover.

```sql
SELECT count(*) AS unknown_role_count
FROM (
  SELECT "RoleName" AS role_name FROM staff_role_assignments
  UNION ALL SELECT "RoleName" FROM staff_money_caps
  UNION ALL
  SELECT btrim(role_name)
  FROM staff_invites,
       LATERAL regexp_split_to_table("RoleNamesCsv", ',') role_name
) roles
WHERE role_name NOT IN
  ('owner', 'branch_manager', 'shift_supervisor', 'cashier_operator',
   'technician', 'accountant_auditor');
```

Do not edit an unknown value in place during the window. Identify its business
meaning, add an explicit migration mapping and test, rebuild the release, and
repeat the rehearsal.

## Maintenance And Backup

1. Announce maintenance and block all mutating ingress to the Platform API.
   Keep only health/readiness probes available. Stop background workers that
   can write money, session, POS, device, update, or identity state.
2. Wait for in-flight mutations to finish and confirm no mutating requests or
   database writers remain.
3. Take a fresh full database snapshot and a verified custom-format logical
   backup. Record both identifiers in the release record and confirm
   `pg_restore --list` can read the dump.
4. Keep mutating traffic stopped until all post-cutover smoke checks pass.

## Apply The Compatibility Unit

Generate and review the idempotent migration script from the approved SHA:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations script `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --idempotent `
  --output artifacts/ef-migrations/platform-organization-cutover.sql
```

Then, in this order:

1. Apply the reviewed migration to the stopped production database.
2. Deploy the Platform API image from the release record.
3. Deploy the Platform Control image from the same release SHA.
4. Publish the recorded Organization Admin MSI and `organization-admin`
   update metadata.
5. Set `OrganizationAdminCompatibility__RequiredEpoch=2` and the approved MSI
   download URL. Restart/roll the API only within this coordinated release.
6. Verify `/api/health` and Platform Control `/healthz` before opening traffic.

The migration maps every known organization role and revokes all platform,
organization-staff, and player access/refresh tokens. Device credentials remain
valid. A client without all three headers below must receive HTTP `426` with
code `organization_admin_upgrade_required`:

```text
X-AFK4-Product: organization-admin
X-AFK4-Compatibility-Epoch: 2
X-AFK4-Client-Version: <installed version>
```

## Smoke Gate

Keep general mutating traffic closed. Use dedicated smoke accounts and one
smoke organization:

1. Complete Account Activation through `/account-activation` and
   `/api/account-activation/organization-owner`; verify it returns no browser
   staff session.
2. Sign in a `PlatformAdmin` to Platform Control and verify organization
   lifecycle access.
3. Sign in a `PlatformSupport` and verify bounded support access plus denial of
   platform-admin-only mutations.
4. Sign in an `OrganizationOwner` to Organization Admin and verify
   organization-wide areas and one backend-confirmed safe action.
5. Sign in an `Operator` and verify the floor-map day flow is available while
   owner/settings areas are absent or forbidden.
6. Substitute another organization ID in an organization route and require
   `403`/`404`. Verify platform tokens fail on organization endpoints and
   organization tokens fail on platform endpoints.
7. Verify pre-cutover platform, organization-staff, and player tokens fail;
   verify each audience can sign in again. Verify one enrolled device still
   authenticates without re-enrollment.
8. Verify an old Organization Admin build receives `426` and the current MSI
   can update, launch, and connect.

Save status codes, timestamps, release identities, and sanitized evidence in
the release record. Do not save credentials or raw tokens.

## No-Return Point

The no-return point is declared only after every smoke item passes and mutating
traffic is reopened. Record its timestamp. After that point, the new release
may have written data under the new identity model; application-only rollback
is forbidden.

Before the no-return point, any failed gate means rollback. After it, stop
traffic and escalate before choosing between a forward repair and the complete
snapshot rollback below.

## Complete Rollback

Never run the previous application against the migrated database and never run
the new application against the restored old database.

1. Stop all mutating ingress and every background writer.
2. Preserve failed-release logs and identifiers without secrets.
3. Restore the entire fresh pre-cutover snapshot, not selected tables.
4. Deploy the previous Platform API and Platform Control image digests together.
5. Restore the previous Organization Admin package/update metadata and previous
   compatibility policy as one unit.
6. Smoke health, previous sign-in, a representative organization read, device
   authentication, and financial/session consistency.
7. Reopen traffic only after the database and every application component are
   confirmed to belong to the same previous release.

Document the failure and repair the release candidate outside the maintenance
window. Never bypass the role preflight, token invalidation, compatibility
epoch, or authorization checks to force a green cutover.
