# Operator Post-Auth Shift Gate Design

**Date:** 2026-07-15
**Status:** Approved for implementation planning

## Goal

Require shift-capable staff to resolve the branch shift immediately after
authentication, before the Operator App exposes its normal workspace. Keep
staff who do not have shift-opening permission, such as technicians and
accountant/auditors, out of this operational gate. Make permanent permission
denials less noisy by hiding inaccessible navigation sections.

## Product Decisions

- Eligibility is permission-based: the gate applies when the refreshed session
  contains `shifts.open`. It must not hardcode role names.
- The gate runs after an interactive sign-in and after restoration of a native
  authenticated session.
- The backend current-shift endpoint is authoritative. Cached UI state and the
  dashboard projection do not decide whether the gate opens.
- If the branch already has an open shift, the employee proceeds directly to
  the first permitted workspace.
- If no shift is open, the employee must either open one or sign out. The gate
  has no close, escape, backdrop-dismiss, workspace, palette, or hotkey bypass.
- Staff without `shifts.open` skip the gate even when the branch has no open
  shift. Backend permission and open-shift validation continue to protect every
  command independently.
- Navigation sections for which the employee has no relevant permission are
  hidden, not rendered as disabled feature advertisements. A temporary lock
  treatment remains appropriate for prerequisites inside an otherwise permitted
  workflow, but no new generic temporary-lock system is part of this change.
- MVP role-to-permission mappings remain predefined. Custom roles and granular
  permission editing remain future scope; the permission-based design will work
  with them without a role-name migration.

## Approaches Considered

### Recommended: permission-based post-auth state machine

A dedicated hook checks the current shift only when authentication, active
branch resolution, and `shifts.open` are present. A dedicated blocking component
owns starting cash, note, retry, open, and sign-out presentation. This keeps the
root app readable, avoids role coupling, and makes loading/error/opening states
testable in isolation.

### Rejected: hardcode cashier and manager role names

This is simpler initially but contradicts permission-based RBAC, breaks users
with multiple roles, and would need rework when custom roles arrive.

### Rejected: render the shell and disable every workspace

This exposes a flash of operational UI, requires many components to understand
the same transient state, and creates bypass risk through hotkeys, the command
palette, or direct state transitions.

## Architecture

### `usePostAuthShiftGate`

The hook accepts auth status, refreshed auth session, active branch, Operator API
configuration, and a translation/error projection dependency following current
Operator hooks. It exposes a discriminated state:

- `not-required` when signed out, missing a resolved branch, or lacking
  `shifts.open`;
- `checking` while reading `/api/branches/{branchId}/shifts/current`;
- `required` when the authoritative response is empty;
- `opening` while the open command is pending;
- `failed` with an actionable error and retry capability;
- `ready` when an open shift exists or has just been confirmed.

The effect must ignore late responses after sign-out, branch change, or session
replacement. Dependencies use stable primitive session/branch identifiers so
unrelated renders do not repeat the check.

### `PostAuthShiftGate`

The component is rendered instead of the Operator shell for `checking`,
`required`, `opening`, and `failed`. It provides:

- a non-dismissible loading state during the authoritative check;
- starting cash, default opening note, and a primary `Open shift` action;
- an explicit `Sign out` secondary action;
- inline actionable failure text and `Retry check` after a read failure;
- disabled inputs and duplicate-submit protection while opening.

The opening request reuses the existing typed shift client, money parser,
idempotency-key helper, active organization/branch, and configured currency.
Starting cash accepts zero and rejects negative or malformed values. A successful
open response changes the gate to `ready`; only then is the Operator shell
mounted.

If another employee opens the branch shift between the check and submit, the
client rechecks current shift after the failed open command. An authoritative
open shift releases the gate; otherwise the original projected error remains.

### Navigation permissions

`WorkspaceRail` filters out sections for which none of their workspaces pass
`canOpenWorkspace`. Workspace tabs retain their existing permission filter.
The command palette and programmatic navigation continue to use the same
permission helpers, so hiding navigation does not become an authorization
boundary.

## Data Flow

1. Native auth completes or restores and refreshes the staff session.
2. The app resolves the active branch and checks `shifts.open` in the refreshed
   permission list.
3. Eligible staff trigger `GET /api/branches/{branchId}/shifts/current`.
4. An existing shift releases the gate. An empty response displays the required
   open form.
5. Submit sends the existing idempotent open-shift command with organization,
   branch, starting cash, currency, note, and a fresh idempotency key.
6. Only a backend-confirmed open shift mounts the regular shell and floor-map
   data flows.

No backend contract or database migration is required.

## Error And Recovery Behavior

- Current-shift read failure blocks entry and offers retry or sign-out; it must
  not assume that the shift is closed.
- Open-shift validation or network failure preserves form input and offers
  another submit after the request finishes.
- A detected concurrent open releases the gate after the authoritative recheck.
- Sign-out clears gate state through the normal auth lifecycle.
- Switching tenant or branch starts a new check and invalidates the previous
  response.

## Testing

Follow TDD with failing tests observed before production changes.

- Hook/component tests cover not-required, checking, existing shift, required
  form, validation, successful open, read failure/retry, submit failure,
  concurrent open reconciliation, sign-out, and stale-response protection.
- App integration tests prove the gate appears after interactive sign-in and
  native session restore, prevents the floor map from mounting, skips staff
  without `shifts.open`, and releases only after backend confirmation.
- Navigation tests prove permanently unauthorized sections are absent while
  partially permitted sections remain available.
- Focused Operator Web tests and a production frontend build are required.
  Because root auth wiring and shared navigation change, the complete Operator
  Web test suite is required before publication.

## Out Of Scope

- Custom role creation or per-user permission checkboxes.
- Backend authorization changes or a new shift API.
- Automatic shift closure or ownership transfer.
- Requiring technicians or accountant/auditors to open a shift.
- A global framework for visually locking every action with unmet business
  prerequisites.
