# Operator System Status Footer

**Date:** 2026-07-15
**Status:** Approved design, pending implementation plan

## Goal

Replace the simplified Operator App footer with a dense system status bar that
shows the authenticated operator and the current runtime context at a glance.
Every displayed identity, role, club, server, and version value must come from
an authoritative source; the UI must not infer roles from permissions or ship a
hard-coded application version.

## Visual Structure

The footer remains one restrained horizontal row, approximately 32 px high,
using the existing shell colors, type scale, semantic status colors, and border
tokens.

```text
Operator: Ivanov I.I. | Role: Cashier | Club: Arena        • Online | Server: OK | Ver. 2.45.1 | 16:45
```

- The left cluster contains operator, role, and club.
- The right cluster contains realtime connectivity, backend availability,
  installed Operator App version, and local time.
- Thin vertical dividers separate fields. The footer has no card surfaces,
  shadows, icons, cash summary, or competing emphasis.
- Labels use the secondary foreground; values use the primary foreground.
- `Online` and `Server: OK` use the existing success tone. Reconnecting uses
  warning; offline or unavailable uses danger. Text accompanies every color.
- Long values truncate without changing row height and expose their full value
  through the native browser tooltip.
- At narrow supported desktop widths, the right cluster remains visible and
  the left cluster truncates in this order: club, roles, operator. The footer
  does not wrap.

## Authoritative Data Sources

### Operator and roles

- `StaffSignInResponse` gains `RoleNames` from the staff role assignments used
  to build the authenticated `StaffContext`.
- Roles are included on sign-in and refresh, stored in
  `OperatorTokenSnapshot`, and projected through `OperatorWebAuthSession` to the
  React `OperatorAuthSession`.
- Multiple roles are ordered consistently and rendered as localized labels,
  separated by commas. Unknown future role identifiers remain visible as their
  raw backend value instead of being silently hidden.
- Existing permission checks remain unchanged and authoritative. Role display
  is informational and must never replace permission authorization.

### Club

- The footer uses the current authoritative floor-map `branchName` for the
  active branch.
- While the first floor-map load is pending, the value is an em dash. The footer
  does not mix stored connection copy with the current backend projection.

### Connectivity and server

- `Online` reflects the existing Operator SignalR connection state.
- `Server: OK` reflects the existing backend data-source state. Fixture/cache
  data must not be presented as a healthy server connection.
- Full connection and data-source details remain available in the tooltip.

### Installed version

- The Windows Operator host reads its own informational version, falling back
  to the assembly version only when informational metadata is absent.
- `OperatorWebBootstrapScript` includes this installed version in
  `window.__AFK4_OPERATOR_CONFIG__`.
- Browser preview uses an explicit development runtime version supplied by the
  preview config, visually labelled as development rather than pretending to
  be an installed package.

### Time

- The footer renders local workstation time in the active locale using a
  minute-aligned timer.
- It updates when the window regains visibility so resume from sleep does not
  leave stale time.

## Component Boundaries

- `ShellStatusBar` receives a small display model rather than reading backend
  clients or native bridge state itself.
- A pure footer projection maps session roles and shell state into display
  fields and semantic tones. This keeps localization and fallback behavior
  testable without rendering the full app.
- `App` supplies the authenticated session, current floor-map branch name,
  realtime state, backend source, and bootstrap version.
- The existing one-shot workspace feedback remains available, but appears as a
  temporary message between the left and right clusters without displacing the
  permanent fields.

## Contract Compatibility

- Adding `RoleNames` to `StaffSignInResponse` is an additive shared-contract
  change and must update every staff auth consumer and fixture.
- Existing protected token snapshots created before this change deserialize
  with an empty role list; the next successful refresh repopulates real roles.
- Adding `appVersion` to the Operator bootstrap payload is additive. The React
  config treats it as optional for compatibility with an older installed host
  during rolling upgrades and shows an em dash until the host is updated.

## Error And Empty States

- Missing operator, role, club, or version values render as an em dash; the UI
  never substitutes a fabricated name, role, club, or production version.
- Offline and server-unavailable states remain explicit text with semantic tone.
- Failure to refresh roles follows the existing auth refresh behavior; the
  footer does not make an independent identity request.

## Verification

- Shared-contract and staff-token tests prove sign-in and refresh return real,
  ordered role names.
- Operator host tests prove roles survive protected snapshot round-trips and
  the bootstrap exposes the installed version.
- React unit tests cover multiple/unknown roles, missing fields, server and
  realtime tones, and the minute-aligned clock.
- App integration tests prove the footer receives the authenticated operator,
  real role names, current branch name, backend state, and host version.
- Rendered QA compares the supplied footer reference and the implemented footer
  together at 1920 and 1280 widths in dark and light themes, with no wrapping,
  overlap, or console errors.

## Out Of Scope

- Changing authorization rules or deriving permissions from displayed roles.
- Adding a new profile endpoint solely for the footer.
- Showing cash totals, shift duration, device counts, or environment secrets in
  the system status row.
