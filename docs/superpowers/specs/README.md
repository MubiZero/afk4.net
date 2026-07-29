# Active Architecture Specs

The active architecture source of truth is:

- `2026-05-12-afk4-platform-architecture-design.md`
- `2026-07-28-platform-organization-product-boundary-design.md` — defines the
  current Platform Control and Organization Admin product, identity, role,
  permission, route, and release boundaries.

Approved backlog specs:

- `2026-07-29-platform-managed-client-updates-design.md` — moves signed package
  publication and rollout authority to Platform Control/release automation,
  adds deterministic batching and maintenance-aware Organization Admin updates,
  and replaces fake rollback with verified last-known-good recovery.
- `2026-07-28-operator-unified-admin-parity-closure-design.md` — closes the
  required Clients, Monetization, Settings, and Venue gaps; its certified
  Platform Control `/club` removal is complete on the current topic branch.
- `2026-07-15-operator-reports-workspace-consolidation-design.md` — redesigns
  Reports as `Сводка / Смены и касса / Выручка`, gives Cash, Events, and Stock
  one clear ownership model, and replaces the separate approvals inbox with
  contextual second-manager confirmation using an existing AFK4 account.
- `2026-06-11-productionize-client-installer-design.md` — shared bundled .NET
  runtime (framework-dependent apps + WiX Burn bundle) to cut the ~160 MB agent
  MSI, channel-driven prod URL, and code signing (blocked on a cert). Includes a
  phased implementation outline; pick up in a new session.
- `2026-06-18-online-booking-autoconfirm-hold.md` — Slice 1 auto-confirmation
  is shipped; Slice 2 wallet hold/no-show release remains backlog and must be
  implemented together with the customer self-service tariff/package picker.

All focused design specs for shipped work — the platform-control redesign, the
2026-06-01 UX-audit feature specs (counter-loop, anti-fraud, offline,
customer portal/shell, notifications, localization, realtime,
frontend-consolidation), the dcgate payments design (incl. the shared-AFK4
Telegram-app reversal that superseded per-owner credentials), the
phone/email staff-identity wave, the brand-positioning copy sweep, the
customer-shell WebView2 pivot and its Unit F cycles (shop, loyalty, news),
shift-revenue + branch-timezone reporting, Operator UI consolidation/QA, and
the commerce/booking financial-integrity wave, plus the Operator cash-terminal
redesign and authoritative system footer — are implemented and archived
under `docs/archive/superpowers/specs/`. Read them for the design rationale
behind already-merged features.

When you start new design work, add the spec here, then move it to the
archive once the work lands on `main`.
