# Plan Navigation

Implementation plans for completed work are archived once their work lands on
`main`. The active plans are:

- `2026-07-15-operator-reports-workspace-consolidation.md` — implements the
  approved Reports center, Cash/Events/Stock ownership model, and secure
  contextual second-manager confirmation before removing the legacy approvals
  inbox.
- `2026-06-11-installer-shared-runtime-workstream-a.md` — shared-runtime client
  installer workstream; blocked on the production signing/certificate decision.

The SP3 admin/billing redesign, the whole SP4 wave (counter-loop, anti-fraud,
offline-resilience, customer portal/shell, notifications, localization,
realtime, dcgate payments), the phone/email staff-identity wave, the
customer-shell WebView2 pivot + Unit F (shop/loyalty/news), shared-AFK4
Telegram payments, shift-revenue reporting, Operator UI consolidation/QA, and
the commerce/booking financial-integrity wave are implemented and merged. The
Operator cash-terminal redesign and authoritative system footer are implemented
and archived on their completed topic branch pending integration. The
2026-07-14 native Operator staging day-flow smoke and its P0 financial fixes are
also complete and archived.

When you start new work, add its plan file here, then move it to the archive
once it ships.

## Archive

All shipped phase, redesign, and SP3/SP4 plans live in
`docs/archive/superpowers/plans/`. Use them when you need the original
implementation context or design rationale for already-merged work.

## Related

- Architecture source of truth: `../specs/2026-05-12-afk4-platform-architecture-design.md`
- Operational/production roadmap: `../../roadmap/production-readiness.md`
- Current-state snapshot: `../../progress/2026-05-12-vertical-slice-progress.md`
