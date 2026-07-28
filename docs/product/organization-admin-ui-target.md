# AFK4 Operator App UI/UX Target

Status: UI/UX target for the active Operator App redesign
Last updated: 2026-05-20

## Purpose

This document records the visual UI and workflow UX target for the native
Windows Operator App. Future Operator App work should converge toward this
target unless the PRD or architecture spec intentionally changes.

The target is a dense, modern operator console for computer-club staff. It is
not a marketing dashboard, not a web admin panel, and not a generic CRUD shell.
The app should feel fast, serious, and safe for repeated cashier/operator use.

## Target Screen

The primary screen after sign-in remains the floor map.

The accepted target layout is:

- top command bar with AFK4 identity, command/player/seat search, realtime
  connection state, open-shift state, branch, operator, and sign-out;
- compact left rail for workspaces: floor map, dashboard, booking, POS/shop,
  clients, payments/shift, logs, and operations/settings;
- central working area focused on the floor map;
- right context panel for the selected seat, active session, billing mode,
  command status, and device state;
- bottom operational signals strip for recent events, errors, cash/POS signals,
  and device warnings.

The visual reference produced on 2026-05-20 used this structure:

```text
+--------------------------------------------------------------------------+
| top bar: brand, command search, realtime, shift, branch, operator         |
+----+-----------------------------------------------+---------------------+
|nav | floor metrics, filters, dense seat map         | selected seat       |
|rail|                                               | session controls    |
|    |                                               | billing/device      |
|    |                                               | command status      |
+----+-----------------------------------------------+---------------------+
|    | operational signals / recent exceptions / cash and device alerts     |
+----+--------------------------------------------------------------------+
```

## Floor Map

The floor map is the main operator workspace and must be optimized for scanning.

It should show:

- branch and zone context;
- counts for free, active, pending, offline, and cash/shift state;
- filter chips for all, free, active, and problem seats;
- dense seat tiles with stable dimensions;
- selected-seat emphasis without resizing the grid;
- explicit state colors for free/ready, active, pending command, warning/debt,
  offline, and maintenance/service;
- player or guest name when available;
- short device/session metadata, such as lock state, game/app, billing source,
  Agent/Shell version, or heartbeat age;
- remaining time or required operator action.

Seat tiles must not expose raw GUIDs in the normal operator path. GUIDs are
acceptable only in advanced technician/debug surfaces.

## Selected Seat Panel

The right panel is the operator's action surface for the selected seat.

It should show:

- seat name and zone;
- device and Shell/Agent summary;
- tabs or segmented context for seat, player, and POS;
- active-session timer with lease/realtime confidence;
- fast extension actions, such as +15 and +30 minutes;
- transfer and end-session actions;
- billing mode selection: fast guest/no-ledger, wallet, package, or postpaid
  where permissions and selected player allow it;
- current money impact in the configured currency, defaulting to TJS;
- explicit pending backend confirmation;
- explicit device command status.

Critical session, money, POS, and device actions must continue to wait for
authoritative backend confirmation. Realtime updates are visible context, not
final confirmation for critical actions.

## Secondary Workspaces

The same operator-console style should apply beyond the floor map:

- Dashboard should summarize the active shift, operator, revenue, tasks,
  services/goods sold, active users, account pressure, and quick actions without
  replacing the floor map as the first AFK4 screen;
- Booking should show active reservations, online requests, strict reservation
  state, create/edit/cancel flows, seat availability, and conflict messages;
- POS should prioritize product selection, cart clarity, payment method, last
  sale/receipt state, refunds, and voids;
- Players should prioritize search, selected-player identity, wallet/debt,
  packages, top-up, and debt payment;
- Cash should prioritize current sales, the active shift, cash operations,
  receipts, and permitted refund/void actions;
- Reports should provide a calm exception-first summary, historical shift/cash
  accountability, revenue with sales/gameplay sources, and filtered exports;
- Events should provide one immutable feed with period/category filters,
  universal search, and object/subject history detail;
- Operations/settings should keep pilot setup, devices, diagnostics, audit,
  updates, tariffs, POS catalog, staff, and layout management organized by task.

These screens can have more forms than the floor map, but the primary operator
path should still avoid raw backend-shaped forms.

The broader secondary-workspace target is based on the public SmartShell
operator/admin structure: Dashboard, Gaming stations/Map, Booking, Shop,
Payments, Clients, Logs, and Settings. AFK4 should borrow the workflow density
and navigation model, while keeping AFK4-specific backend confirmation,
multi-tenant permissions, and native desktop/WebView2 runtime boundaries.

## Visual Direction

The target style is modern, restrained, and operational:

- light main workspace with dark top bar and dark left rail;
- compact spacing and high information density;
- 6-8 px corner radii for cards, tiles, buttons, chips, and panels;
- crisp borders and subtle shadows only where they improve separation;
- status color system with blue for ready/primary, green for active/success,
  amber for pending/warning, red for blocking errors, gray for offline/neutral,
  and violet only for pending or system-command states;
- Russian-first operator copy for the primary day flow;
- no oversized hero sections, decorative gradients, marketing cards, or
  empty illustration-driven dashboard areas.

## Acceptance Checklist

Future Operator App UI work should be reviewed against this checklist:

- Floor map is the first useful screen after sign-in.
- A trained operator can start, extend, transfer, and end a normal session from
  the floor map context without navigating through raw forms.
- Pending and failed backend/device states are obvious.
- Normal cashier/operator paths do not require copying GUIDs.
- POS, player, shift, and selected-seat money operations display the configured
  currency.
- Russian-first labels are used in the primary operator day flow.
- Device online/offline, lock state, heartbeat recency, and command state are
  visible enough for troubleshooting.
- Critical actions remain blocked on backend confirmation.
- The UI stays dense and readable at the current native desktop minimum size
  constraints.
- Advanced technician/debug data is available where needed, but separated from
  the normal operator path.
