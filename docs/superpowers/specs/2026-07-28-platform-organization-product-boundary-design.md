# Platform And Organization Product Boundary Design

**Status:** approved

**Date:** 2026-07-28

**Migration strategy:** Big Bang without compatibility aliases

## Purpose

AFK4 currently uses `owner`, `Operator App`, and `Control Plane` in ways that
can blur two unrelated security domains: the AFK4 team administering the SaaS
platform and a customer administering one organization. This design gives the
two domains explicit product names, actors, authentication boundaries, API
namespaces, and release rules.

The change is intentionally semantic, not cosmetic. An unqualified `owner`
must no longer be able to mean either a platform administrator or the owner of
a customer organization.

## Canonical Product Vocabulary

| Previous term | Canonical term | Meaning |
| --- | --- | --- |
| SaaS Control Plane / Control Plane | **Platform Control** | Internal browser product used by the AFK4 team |
| Platform Owner | **Platform Admin** | AFK4 employee with platform administration authority |
| Platform Support Staff | **Platform Support** | AFK4 employee with bounded, audited support authority |
| Operator App | **Organization Admin** | Native Windows product used to operate and administer a customer organization |
| Owner / Club Owner | **Organization Owner** | Customer role with authority inside one organization |
| Operator | **Operator** | Customer staff role only; never a product name |
| Tenant | **Organization** in user-facing language | Customer business and data-isolation boundary |
| Club location | **Branch** | One physical club or site belonging to an organization |

Product names do not repeat the AFK4 brand in ordinary navigation and prose.
Installers and operating-system surfaces may use `Organization Admin by AFK4`
where publisher identity is useful.

Technical projects retain the `AFK4` namespace:

- `AFK4.PlatformControl.Web`
- `AFK4.OrganizationAdmin.App`
- `AFK4.OrganizationAdmin.Web`

`Platform Control` and `Organization Admin` are the only current product
names. Historical names may remain in archived evidence but must not appear as
current terminology.

## Product Boundaries

### Platform Control

Platform Control treats organizations as customers of AFK4. It owns:

- organization provisioning and lifecycle status;
- first-branch provisioning;
- SaaS plan, subscription status, limits, suspension, and reactivation;
- the first Organization Owner invitation;
- platform-wide health and support diagnostics;
- bounded cross-organization support access;
- audit of Platform Admin and Platform Support actions.

Platform Control is not used for a customer's daily operations. It must not
accept organization staff tokens or infer organization permissions from an
AFK4 employee account.

### Organization Admin

Organization Admin treats one organization as the user's business boundary.
Its permission-controlled areas include:

- organization profile and settings;
- branches, halls, seats, devices, and Agent installation;
- staff and organization roles;
- club tariffs, packages, goods, payment providers, and loyalty;
- shifts, cash, players, sessions, and POS;
- organization and branch reports;
- the organization's own audit journal;
- viewing and paying the organization's AFK4 subscription.

Organization Admin must not expose other organizations or internal platform
operations. The main floor map remains the primary workspace for operational
roles even though the product is no longer named after the Operator role.

## Actors And Authorization

### Platform actors

- `PlatformAdmin` has platform administration permissions.
- `PlatformSupport` has only explicit support permissions.
- Future specialized roles may include `PlatformBillingManager` and
  `PlatformAuditor`; they are not required by this migration.

Platform permissions use a `platform.` namespace, for example:

- `platform.organizations.view`
- `platform.organizations.manage`
- `platform.subscriptions.manage`
- `platform.support.access`
- `platform.audit.view`

### Organization actors

- `OrganizationOwner`
- `BranchManager`
- `ShiftSupervisor`
- `Operator`
- `Technician`
- `Accountant`

Organization permissions use an `organization.` namespace, for example:

- `organization.branches.manage`
- `organization.staff.manage`
- `organization.devices.manage`
- `organization.billing.view`
- `organization.reports.view`
- `organization.pos.sell`

`OrganizationOwner` is an organization role governed by RBAC, not a bypass of
authorization checks.

### Token boundary

Every authenticated token has exactly one subject domain: `platform` or
`organization`.

- Platform Control accepts only platform-domain tokens.
- Organization Admin accepts only organization-domain tokens.
- A permission string belongs to exactly one domain.
- Platform Support enters customer context only through an explicit,
  time-bounded, reason-bearing, audited support mechanism.
- Support access does not impersonate an Organization Owner and does not mint a
  normal organization staff session.

## API Boundary

Platform operations live under:

```text
/api/platform/*
```

Organization operations live under:

```text
/api/organizations/{organizationId}/*
```

The authenticated organization claim must match `{organizationId}` unless the
request is handled by the dedicated audited support mechanism. A caller cannot
select another organization merely by changing the route value.

Unqualified `/api/owner/*` routes are removed. Shared transport DTOs may remain
in `AFK4.Shared.Contracts`, but platform and organization API clients,
authentication models, permissions, and endpoint authorization policies remain
separate.

## First Owner Activation

First-owner onboarding is a public system flow rather than a workspace inside
either authenticated product:

1. Platform Admin creates an organization in Platform Control.
2. Platform Control issues a short-lived, one-time, revocable invitation for
   `OrganizationOwner`.
3. The recipient opens **Account Activation**.
4. The recipient validates the invitation and creates their credentials.
5. Account Activation directs them to install Organization Admin.
6. The Organization Owner signs in to Organization Admin and completes branch
   and business setup.

Account Activation does not persist or return a usable organization staff
session to the browser. Platform Admin neither knows nor assigns the owner's
password. Invitations for later owners and staff are managed from Organization
Admin.

## Big Bang Migration

The repository moves to the new model in one coordinated change. There are no
runtime aliases, dual permission strings, legacy endpoint fallbacks, or mixed
product names.

The migration includes:

1. Rename projects, assemblies, namespaces, directories, build targets,
   Docker definitions, CI jobs, installers, package metadata, and runtime
   configuration from Platform Web and Operator App names to Platform Control
   and Organization Admin.
2. Replace current product copy, i18n keys, active documentation, runbooks, and
   architecture references with the canonical vocabulary.
3. Rename ambiguous roles, claims, permission constants, policies, DTOs,
   clients, endpoints, tests, and seeded data.
4. Add a database migration that replaces stored role and permission values
   atomically and rejects ambiguous or unmapped values.
5. Move all platform and organization routes to their canonical namespaces and
   remove the old routes in the same release.
6. Invalidate all existing access and refresh tokens. Every user signs in
   again after the release.
7. Release the backend, Platform Control, Organization Admin, and Organization
   Admin installer as one compatibility unit.

Old Organization Admin predecessors are incompatible after the cutover and
must be blocked through the existing minimum-version/update mechanism. Partial
deployment is not supported.

## Data Migration Safety

Before production execution, the migration must run against a recent sanitized
copy of the production schema and prove:

- every stored role and permission has one explicit destination;
- no account crosses from the organization domain into the platform domain;
- unknown values abort the migration rather than being silently dropped;
- Organization Owner assignments retain their organization scope;
- Platform Admin and Platform Support assignments retain their platform scope;
- audit history remains readable and preserves the historical actor identity;
- token invalidation is deterministic and does not depend only on client-side
  storage deletion.

Migration logs may contain identifiers and counts but must not expose tokens,
passwords, or other secrets.

## Release And Rollback

The cutover requires a maintenance window. Mutating traffic is stopped before
the database migration begins. Deployment order is coordinated as one release,
not treated as independent backward-compatible services.

Release gates:

- full solution build and test;
- focused authorization and cross-organization isolation tests;
- migration rehearsal and unmapped-value failure test;
- production builds for Platform Control and Organization Admin;
- Docker smoke for Platform Control;
- Windows build, MSI installation, launch, update, and sign-in smoke for
  Organization Admin;
- Account Activation smoke;
- forced reauthentication proof for a previously valid access and refresh
  token;
- smoke of Platform Admin, Platform Support, Organization Owner, and Operator
  permissions.

Rollback is the restoration of the entire previous release together with its
compatible database snapshot. Rolling back only an application or only the
database is unsupported. The release runbook must define the final no-return
point before production approval.

## Acceptance Criteria

- Active code, UI, tests, configuration, build outputs, installers, and current
  documentation use `Platform Control` and `Organization Admin` consistently.
- `Operator` appears only as a staff role or as explicitly marked historical
  evidence.
- Unqualified `Owner` does not identify a role, endpoint, permission, client,
  or auth model.
- Platform and organization tokens are rejected by the opposite product and
  endpoint policies.
- Organization routes enforce token organization scope.
- Cross-organization support access is explicit, time-bounded, reason-bearing,
  and audited.
- All old access and refresh tokens fail after migration.
- Old endpoints and permission aliases are absent rather than deprecated.
- Account Activation completes without creating a browser staff session.
- The coordinated release and complete rollback procedure are verified before
  production deployment.

## Non-Goals

- Building a separate mobile owner application.
- Adding future specialized platform roles before they are needed.
- Changing the modular-monolith backend decision.
- Changing Organization Admin's floor-map-first operational UX.
- Preserving compatibility with old product binaries, endpoints, tokens, or
  permission strings after the cutover.
