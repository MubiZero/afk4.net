# Operator Pilot Setup UI Design

Date: 2026-05-19

## Purpose

Add a minimum operator-facing pilot setup panel to the native Operator App.
The panel turns the existing `scripts/configure-pilot-branch.ps1` workflow into
an in-app Settings workflow so a branch manager or owner can prepare a pilot
branch without direct PostgreSQL edits and without a web admin panel.

## Context

The backend already exposes the required API surface:

- `POST /api/branches/{branchId}/staff`
- `GET /api/branches/{branchId}/staff`
- `POST /api/branches/{branchId}/layout/zones`
- `GET /api/branches/{branchId}/layout/zones`
- `POST /api/branches/{branchId}/layout/seats`
- `POST /api/branches/{branchId}/tariffs`
- `POST /api/branches/{branchId}/tariffs/{tariffId}/versions`
- `POST /api/branches/{branchId}/pos/categories`
- `POST /api/branches/{branchId}/pos/products`
- `POST /api/branches/{branchId}/devices/{deviceId}/seat-assignment`

The current Operator App Settings workspace already gates panels by permissions
and hosts technician device tools, update status, audit, and diagnostics. The
pilot setup UI should follow that pattern instead of becoming a new top-level
workspace. The original slice was implemented in WPF; the go-forward Operator
App runtime is WebView2 + React/TypeScript, so this workflow must be ported as
part of Operator App UI migration.

## Scope

The first UI slice creates one `Pilot Setup` Settings panel. It covers:

- create or reuse branch staff users for cashier, technician, and supervisor
  roles;
- create or reuse one zone and a configurable number of seats;
- create one tariff and active tariff version;
- create one POS category and one POS product;
- optionally assign an existing enrolled device to one configured seat;
- show step-by-step progress, created/reused identifiers, and the first
  actionable error.

The panel is intentionally not a full configuration console. It does not edit
custom roles, delete data, manage production secrets, or replace the existing
technician/update/audit panels.

## User Experience

The panel appears under Settings as `Pilot Setup` when the signed-in staff user
has at least one setup permission from the required set. The main content is a
dense operational form, not a marketing-style wizard:

- branch context is read from the signed-in Operator App context;
- form fields default to pilot-safe values matching the runbook;
- staff rows expose username, display name, password, and predefined role;
- layout fields expose zone name, seat prefix, seat count, and starting sort
  order;
- tariff fields expose name, currency, price per minute, minimum billable
  minutes, rounding increment, and effective date;
- POS fields expose category name, product name, SKU, price, stock tracking,
  and negative stock setting;
- optional device assignment fields expose device id and target seat name;
- `Apply Pilot Setup` executes the checked sections in order;
- a results list shows `pending`, `running`, `created`, `reused`, `skipped`, or
  `failed` for each step.

The workflow runs as one user action, but it is internally step-based so
operators can tell exactly where a setup failed and rerun after correction.

## Permissions

The panel is visible when any of these permissions are present:

- `identity.branch_staff.manage`
- `layout.manage`
- `tariffs.manage`
- `pos.catalog.manage`
- `devices.seat_assignment.assign`

Each section is enabled only when its matching permission is present. A branch
manager who lacks device assignment can still create staff, layout, tariff, and
POS setup. A technician who only has device assignment can assign an existing
device without seeing staff/password fields.

## Application Structure

Add a focused Operator App module under `src/AFK4.Operator.App/PilotSetup`:

- `IOperatorPilotSetupApiClient` defines the exact API operations the panel
  needs.
- `HttpOperatorPilotSetupApiClient` sends authenticated requests and reads
  shared contract DTOs.
- `UnconfiguredOperatorPilotSetupApiClient` matches existing app fallback
  clients.
- `PilotSetupWorkspaceViewModel` owns form state, validation, command state,
  execution order, and step result projection.
- Small immutable view models or records represent staff rows and setup step
  results.

`SettingsWorkspaceViewModel` receives an optional `PilotSetupWorkspaceViewModel`
and exposes `HasPilotSetup`. It adds the `pilot-setup` panel when permissions
allow it and forwards organization/branch context through `ApplyContext`.

`MainWindow.xaml` renders the new panel inside the existing Settings area. The
XAML should reuse the current dense Settings layout, DataGrids where useful,
and existing command/status conventions.

## Data Flow

`PilotSetupWorkspaceViewModel.ApplyAsync` validates the form before making API
calls. It then executes enabled sections in this order:

1. staff users;
2. layout zone;
3. layout seats;
4. tariff;
5. tariff version;
6. POS category;
7. POS product;
8. optional device-seat assignment.

The view model uses stable idempotency keys derived from branch id and trimmed
business values for idempotent endpoints. Staff, zone, and seat steps first
read existing data where the backend exposes a list endpoint; matching records
are reported as `reused`.

The device assignment step only runs when both a device id and target seat name
are present. It uses the seat id produced or reused by the layout step.

## Error Handling

Validation errors are local and do not call the API. They set the workspace
error message and mark the relevant step as `failed`.

HTTP and API errors are caught at the step boundary. Earlier successful steps
remain visible, later steps stay `pending` or `skipped`, and the command
becomes available for a rerun. The UI must not roll back created data because
backend setup operations are append-only or idempotent.

Passwords are never written to logs, progress docs, status text, or result
rows. The panel may keep password fields in memory only for the current
operator action.

## Testing

Use TDD for the implementation:

- API client tests verify every route, HTTP method, bearer token, request body,
  and error behavior.
- Settings workspace tests verify panel visibility, `HasPilotSetup`, and
  context forwarding.
- ViewModel tests verify defaults, permission-gated sections, validation,
  execution order, created/reused/skipped/failed results, idempotency key
  derivation, optional device assignment, and password redaction.
- XAML is kept binding-simple so behavior stays covered in ViewModel tests.

Before a PR is merged, run focused Operator App tests, full solution build, and
remote `PR Verification Result` on the current head commit.

## Documentation

Update `docs/operations/pilot-branch-setup.md` to mention the Operator App
panel as the preferred pilot setup path, with the script remaining as a
release-workstation fallback.

Update `docs/progress/2026-05-12-vertical-slice-progress.md` if the
implementation changes the current pilot setup capability, verification
evidence, or recommended next work.
