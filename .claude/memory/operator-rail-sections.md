---
name: operator-rail-sections
description: "Operator left rail is a sections+tabs model (6 entries, not 14 flat); --shell-tabstrip couples tab height into workspace calc heights; WorkspaceErrorBoundary; dev mock returns [] for unknown routes"
metadata: 
  node_type: memory
  type: project
  originSessionId: 16d55fb3-a7fa-4e36-af4f-29c4ec7b2325
---

Operator console left rail consolidated 14 flat workspaces → **6 sections** with in-section tabs. **Merged PR #84 to main, 2026-06-14.**

- **Model:** `navSections` in `operatorData.ts` (replaces old flat `navItems`/`navGroups`). Each section = `{ key, labelKey, icon, items: NavItem[] }`; `NavItem` carries its own `id: WorkspaceId` (no more fragile positional `workspaceIds[index]` coupling). Standalone (1 item): Карта, Брони, Клиенты. Tabbed: **Касса** (pos/shop_orders/payments/review), **Отчёты** (dashboard/shifts), **Управление** (settings/payment_cards/loyalty/news/logs).
- **Behaviour:** rail click → first allowed item; horizontal `.workspace-tabs` strip shows only when >1 allowed item; single allowed → opens straight (no strip). Permissions unchanged (`canOpenWorkspace`): section locks only if ALL its items locked; disallowed tabs hidden one-by-one.
- **Layout gotcha (important):** tabbed screens' inner `calc(100vh - 46px - 26px - …)` heights would slip off-screen by the strip height. Fixed by a `--shell-tabstrip` CSS var (set inline on `.operator-shell`: `41px` when tabs shown else `0px`) folded into ALL those calc rules. If you add a new full-height workspace, include `- var(--shell-tabstrip, 0px)` in its height calc.
- **WorkspaceErrorBoundary** wraps the workspace content, `key={workspace}` so it resets on nav. A single screen crash now shows a localized message (`op.shell.workspaceError`) and keeps rail/header alive instead of blanking the whole shell.
- **Dev-mock gotcha:** `devMockBackend.ts` answers unknown routes with a bare `json([])`. Endpoints whose client expects an OBJECT (e.g. payment gateways `list()` → `{gateways}`) get `undefined` fields → guard with `?? []`. This black-screened PaymentGatewaysWorkspace before the boundary+guard.
- Test nav helper: `App.test.tsx` has `gotoWorkspace(label)` — opens section then tab; use it instead of `getByTitle('<screen>')` for now-tabbed screens.

Related: [[operator-theme-and-preview]], [[operator-wizard-auth-phone-first]].
