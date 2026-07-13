# Plan Navigation

Implementation plans for completed work are archived once their work lands on
`main`. The active plans are:

- `2026-07-13-operator-ui-history-consolidation.md` — verify a normal merge of
  the final Operator UI tree, then reproduce that exact tree on current main
  as one clean consolidation commit.
- `2026-07-13-shop-orders-pos-financial-integrity.md` — settle new Player Shop
  orders as linked paid POS sales with wallet, receipt, shift, refund, and
  weighted-average inventory-cost integrity.

The SP3 admin/billing redesign, the whole SP4 wave (counter-loop, anti-fraud,
offline-resilience, customer portal/shell, notifications, localization,
realtime, dcgate payments), the phone/email staff-identity wave, the
customer-shell WebView2 pivot + Unit F (shop/loyalty/news), shared-AFK4
Telegram payments, and shift-revenue reporting are implemented and merged.

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
