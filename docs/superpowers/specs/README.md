# Active Architecture Specs

The active architecture source of truth is:

- `2026-05-12-afk4-platform-architecture-design.md`

Open epic pending written-spec review:

- `2026-07-14-operator-commerce-booking-ux-completion-design.md` — integrate
  the consolidated Operator UI with the verified commerce core, finish POS
  split settlement/refunds, client selection, order/stock polish, arbitrary
  booking seat selection, and atomic reservation-to-session start. Online
  booking money holds/no-show remain a separate epic.

Open epic, approved for planning (not yet started):

- `2026-07-13-shop-orders-pos-financial-integrity-design.md` — route every new
  Player Shop order through a linked paid POS sale, wallet payment, receipt,
  shift, and weighted-average inventory-cost path while retaining legacy-order
  cancellation compatibility.
- `2026-06-11-productionize-client-installer-design.md` — shared bundled .NET
  runtime (framework-dependent apps + WiX Burn bundle) to cut the ~160 MB agent
  MSI, channel-driven prod URL, and code signing (blocked on a cert). Includes a
  phased implementation outline; pick up in a new session.
- `2026-07-13-operator-qa-hardening-design.md` — Operator App CTA and contrast
  remediation plus reliable browser-preview data and realtime behaviour.
- `2026-07-13-operator-ui-history-consolidation-design.md` — preserve the
  verified final Operator UI tree while consolidating its iterative history
  onto the latest `origin/main` without touching legacy dirty worktrees.

All focused design specs for shipped work — the platform-web redesign, the
2026-06-01 UX-audit feature specs (counter-loop, anti-fraud, offline,
customer portal/shell, notifications, localization, realtime,
frontend-consolidation), the dcgate payments design (incl. the shared-AFK4
Telegram-app reversal that superseded per-owner credentials), the
phone/email staff-identity wave, the brand-positioning copy sweep, the
customer-shell WebView2 pivot and its Unit F cycles (shop, loyalty, news),
and shift-revenue + branch-timezone reporting — are implemented and
archived under `docs/archive/superpowers/specs/`. Read them for the design
rationale behind already-merged features.

When you start new design work, add the spec here, then move it to the
archive once the work lands on `main`.
