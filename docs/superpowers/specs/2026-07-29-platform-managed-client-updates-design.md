# Platform-Managed Client Updates Design

Status: Approved for implementation
Date: 2026-07-29

## Purpose

Make AFK4 client updates safe for unattended commercial operation and align
release authority with the product boundary already defined for Platform
Control and Organization Admin.

This design covers three sequential workstreams:

1. platform-owned package publication and deterministic rollout;
2. maintenance-aware Organization Admin installation;
3. verified last-known-good recovery after a broken upgrade.

Each workstream must be independently releasable and verified before the next
one starts. The existing stable `latest` Organization Admin URL remains only a
compatibility-gate download target. Managed updates continue to use immutable,
versioned artifact URLs.

## Product Boundary

### Platform Control and release automation

The AFK4 platform owner owns the software supply chain. Only Platform Control
and trusted release automation may:

- register signed client package metadata;
- validate or retire a package;
- create, pause, resume, complete, cancel, or roll back a rollout;
- select organizations, branches, devices, channels, and batch percentages;
- inspect rollout status across tenants.

Package registration and rollout mutation use the platform-admin authorization
boundary. CI uses a dedicated platform release credential with only the update
release permissions; it must not authenticate as organization staff.

### Organization Admin

Organization Admin is the club-operation client. It must not accept artifact
URLs, hashes, signatures, package identifiers, rollout targets, or release
state transitions from organization staff.

Its update surface is read-only apart from local installation preferences. It
shows the installed and offered versions, rollout state, download/install
progress, useful failure details, and the configured maintenance window.
Authorized organization staff may change that maintenance window but cannot
change package or rollout authority.

### Agent

The Windows Agent Service remains the only trusted client-side update
executor. Organization Admin never installs its own MSI and never becomes an
authority for package eligibility.

## Platform Package Catalog

Update packages become platform-global release records rather than records
owned by an organization branch. A package contains:

- component, semantic release version, and channel;
- immutable HTTPS artifact URI;
- byte length and SHA-256;
- ECDSA P-256 signature and algorithm;
- release notes and release lifecycle state;
- creation, validation, retirement, and actor audit metadata.

The supported lifecycle is `registered -> validated -> retired` or
`registered -> rejected`. Only `validated` packages may be referenced by a new
rollout. Existing active rollouts stop offering a package immediately if that
package is retired.

The migration is a big-bang development cutover: there are no customer records
to preserve. The old organization/branch-owned update package and staff update
mutation endpoints are removed rather than kept as compatibility aliases.

## Rollout Model

A rollout references one validated platform package and defines:

- target scope: organizations, branches, or explicit devices;
- channel and start time;
- integer batch percentage from 1 through 100;
- state and audited reason;
- optional maintenance-window policy for Organization Admin.

Rollout mutation belongs to Platform Control. Organization Admin consumes
status only.

### Deterministic batching

Eligibility uses a stable SHA-256 bucket derived from rollout ID and device ID.
The first unsigned 32-bit value maps to bucket `0..99`; a device is eligible
when its bucket is less than `BatchPercent`. The same rollout and device always
produce the same result on every API instance and after restart. Increasing the
percentage only adds devices and never removes already eligible devices.

Explicit device-target rollouts still apply batching to their target set.
`100` includes every otherwise eligible device. Tests use known rollout/device
IDs with asserted buckets so the algorithm cannot drift accidentally.

### Selecting an instruction

For each component, the backend returns at most one instruction per update
check: the highest eligible version newer than the installed version. A newer
active rollout supersedes an older eligible rollout for the same component.
The backend never asks a device to install a chain of intermediate versions in
one check.

Pausing a rollout prevents new instructions immediately. An Agent that already
started an installation completes its current transaction and reports the
result.

## Organization Admin Maintenance Lifecycle

### States

Organization Admin updates add explicit `deferred`, `ready-to-install`,
`awaiting-app-exit`, `health-checking`, and `rollback-required` states alongside
the existing download, install, success, and failure states.

### Behavior

- If Organization Admin is not running, Agent may install a verified update
  immediately.
- If it is running outside the maintenance window, Agent reports `deferred` and
  Organization Admin shows the offered version with `Restart and update` and
  `Later` actions.
- `Restart and update` asks the app to save local UI state and exit normally.
- At the maintenance window, Agent requests the same graceful exit. It does not
  terminate the process while a critical operator command is in flight.
- If the app cannot exit safely, Agent leaves the update deferred and retries in
  the next window. It must not convert this expected condition into an install
  failure.
- Once the app exits, Agent installs the MSI and starts the new executable.

Agent and Organization Admin communicate over a machine-local authenticated
named pipe. The protocol exposes current app activity, requests graceful
shutdown for a specific rollout/package, and acknowledges that shutdown state
has been persisted. The pipe does not carry package metadata or grant release
authority.

The app treats an API-backed money, session, POS, device, or shift command as
critical from submission until backend confirmation or failure. Passive
navigation, filtering, and local drafts do not block an accepted user-requested
restart once their local state is saved.

The maintenance window is stored in branch local time using the existing
branch timezone. The initial default is a daily 04:00-05:00 window. An explicit
`Restart and update` bypasses the window but not critical-command safety.

## Last-Known-Good Recovery

The current rollback helper is not a rollback: it receives the newly downloaded
artifact and invokes that same MSI again. It is removed before recovery is
reported as automated.

### Recovery material

After every health-confirmed installation, Agent keeps one verified
last-known-good package per installed component:

- immutable local MSI path;
- component and installed version;
- original immutable artifact URI;
- size, SHA-256, signature, algorithm, and release notes;
- confirmation timestamp.

The metadata and file live under the protected Agent data directory. The cache
keeps only the confirmed package plus the currently staged candidate. An Agent
must verify cached bytes against the stored signed metadata before recovery.

For an existing installation that predates this mechanism, no package is
fabricated. The first upgrade uses Windows Installer transactional rollback;
automated post-install recovery becomes available after one package has passed
health confirmation and entered the known-good cache.

### Health confirmation

After MSI success and Agent configuration reload, Agent launches Organization
Admin with a one-time local health token. The app confirms that the native host,
embedded Web assets, WebView2 initialization, configuration loading, and local
Agent pipe are operational. Authentication and cloud availability are not
required for this local startup health check.

If confirmation does not arrive within the configured timeout, Agent marks the
candidate `rollback-required`, verifies the known-good package, restores it,
restarts itself if necessary, launches the restored app, and reports the final
status. If no known-good package exists, Agent reports a precise manual-recovery
failure and keeps managing the workstation.

The Organization Admin WiX package must support installing the recorded
known-good version during this controlled recovery path. Downgrade permission
must be scoped to the verified Agent recovery command and must not turn a
normal interactive install into an unrestricted downgrade path.

## Security and Failure Handling

- Agent verifies size, SHA-256, signed metadata, component, version, channel,
  and immutable URI before staging or caching an MSI.
- Platform API rejects non-validated packages for rollout creation and does not
  return retired packages.
- Platform release actions and device status changes remain audited.
- Tenant staff tokens receive `403` from all package and rollout mutation
  endpoints after cutover.
- A missing signing key, invalid signature, unsafe URI, failed graceful exit,
  missing rollback point, or failed health confirmation remains visible as a
  specific state; none is collapsed into generic success.
- Stable compatibility aliases never participate in signature payloads,
  managed rollout instructions, or rollback metadata.

## User Interfaces

Platform Control gains an `Updates` area with package catalog, validation,
rollout creation, target summary, deterministic batch percentage, state
controls, and per-device progress. It follows the existing internal admin SPA
patterns and platform permissions.

Organization Admin Settings replaces the current developer package/rollout
forms with a compact status card: installed version, offered version, progress,
maintenance window, `Restart and update`, and actionable failure details. It
does not duplicate Platform Control release administration.

## Migration and Delivery Order

### Workstream A: platform release control

1. Add platform package/rollout contracts, persistence, permissions, endpoints,
   audit, and Platform Control UI.
2. Enforce validated-only packages, retired-package exclusion,
   deterministic batching, and one instruction per component.
3. Move Package Smoke registration to the platform release credential.
4. Remove staff mutation endpoints and Organization Admin release forms.
5. Preserve read-only device update status for permitted organization roles.

This workstream is complete when API, contract, Platform Control, Organization
Admin, workflow, and migration tests pass and package smoke proves a canary
device rollout through the platform boundary.

### Workstream B: maintenance-aware installation

1. Add maintenance preference contracts and persistence.
2. Add the local Agent/App coordination protocol and explicit deferred states.
3. Add notification, postpone, restart-now, graceful-exit, install, and relaunch
   behavior.
4. Prove critical commands prevent automatic shutdown.

This workstream is complete after automated tests and a real Windows smoke with
the app closed, idle, and holding an in-flight critical command.

### Workstream C: known-good recovery

1. Replace the fake rollback adapter with verified known-good storage.
2. Add startup health confirmation and timeout handling.
3. Add controlled MSI recovery support and exact failure reporting.
4. Prove successful upgrade, MSI transactional failure, startup health failure,
   successful recovery, and no-known-good manual recovery on Windows.

## Verification

Required automated coverage includes:

- platform versus tenant authorization for every release mutation;
- package lifecycle and retired-package behavior;
- deterministic batching stability, monotonic widening, and 100% inclusion;
- at most one newest instruction per component;
- immutable URI and signature verification;
- maintenance-window timezone behavior;
- critical-command deferral and graceful shutdown acknowledgement;
- version reporting after Agent restart;
- known-good cache integrity and retention;
- startup health success, timeout, recovery, and precise no-recovery behavior;
- absence of release mutation controls from Organization Admin.

Each workstream requires focused tests and affected production builds. The final
gate requires the full solution build/test, Platform Control and Organization
Admin production builds, package automation tests, an unsigned staging package
smoke, and physical Windows update/recovery evidence. Production rollout still
requires Authenticode signing custody and recorded production rollback proof.

## Documentation Impact

Implementation updates the PRD and architecture spec first where they describe
update authority, then the production-readiness roadmap, current progress,
client update rollout runbook, package publishing runbook, Windows device smoke
runbook, and active plan/spec indexes. No important operational state may remain
only in this design or chat.
