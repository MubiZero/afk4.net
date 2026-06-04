# Platform.Web Club Owner Console (Sub-Project 2) — Design

Date: 2026-05-29
Owner: AFK4 platform
Status: Design (approved in brainstorming; pending written-spec review)

## Why This Exists

Sub-project 1 built the design foundation, the app-shell, and one fully-redesigned
screen (Club Overview). It merged to `main` on 2026-05-29 (commit `5282aa5`). Every
other `/club/*` screen was **reparented** into the new shell but still renders the
old legacy look (`styles.css`, plain tables, marked "будет обновлено"), and several
areas (Clients/CRM, Monetization, Reports analytics, Profile) have no screen at all.

Sub-project 2 brings the **entire** club-owner cabinet to full product quality:
redesign the reparented legacy screens, and build the missing screens. The owner
asked for the opposite of MVP — a modern, complete owner console with good UX, with
SmartShell.gg as the aspirational completeness bar.

This is overwhelmingly a **frontend** effort. A backend inventory confirmed the
server already exposes complete contracts for every area in scope (tariffs,
packages, POS catalog + inventory, players/wallet/ledger/debts, reports with CSV
export, audit, staff roles, branch settings, floor map, devices). No new backend
contracts are planned.

## Locked Decisions (from brainstorming)

### Scope boundary (web vs native Operator App)

The web cabinet is a **full owner control panel**: configuration + analytics +
**management of player money** (wallet top-ups, refunds, manual corrections, debt
payments) — SmartShell-grade. Only **live real-time operations** stay in the native
WebView2 Operator App: counter POS sales, shift open/close, session start/extend,
the live floor-map operation. The architectural rule "no browser web admin as the
primary club UI" is unchanged — the web cabinet is the owner/manager management and
analytics console used remotely, not a second operator console.

### Billing deferred

The "Биллинг" account-group item (the club's own subscription/plan on the AFK4
service — distinct from player wallets) is **deferred to sub-project 3**, because it
depends on the platform/admin plan model that is more naturally designed alongside
the `/admin/*` control plane. The nav item remains a labelled "soon" placeholder.

### Multi-branch

Working screens stay **branch-scoped** via the existing branch switcher. The
**"Все филиалы"** item becomes a real **aggregated overview dashboard** (per-branch
KPI/revenue side by side), aggregated **client-side from existing branch-scoped
endpoints** — no new aggregation endpoints. Per-screen "all branches" mode is out of
scope.

## Information Architecture (unchanged from sub-project 1, now fully routed)

Every nav item below now resolves to a real, redesigned screen (except the two noted).

**Группа «Филиал» (branch-scoped):**

- **Обзор** — done in sub-project 1; not re-touched except polish-backlog fixes.
- **Зал и ПК** — tabs: Карта зала · Устройства · Ожидают подтверждения (tab visible
  only when pending devices exist).
- **Клиенты** — CRM: searchable list + client detail.
- **Монетизация** — tabs: Тарифы · Товары · Лояльность. (owner-only)
- **Отчёты** — tabs: Аналитика · Журнал событий.
- **Настройки** — tabs: Филиал · Операторы и роли. (owner-only)

**Группа «Аккаунт» (account-scoped):**

- **Все филиалы** — aggregated overview dashboard + branch CRUD list.
- **Установка** — done; light cosmetic pass only.
- **Биллинг** — deferred to sub-project 3; remains a "soon" placeholder.
- **Профиль и доступ** — account profile and access.

Role gating is unchanged: owner-only items (Монетизация, Настройки, Биллинг,
Профиль) hidden for `branch_manager`, enforced in routing and render, via the
existing `roleFromPermissions` / `visibleNav`.

## Shared Design Language Additions

The foundation from sub-project 1 (tokens, themes, i18n, shell) is reused as-is.
Sub-project 2 adds the reusable building blocks the table/form/detail screens need.

### New vendored shadcn/ui primitives (`src/components/ui/`)

Added as needed, theme-aware against existing tokens: **Table** (with sorting,
empty, and skeleton states), **Input**, **Select**, **Textarea**, **Switch**,
**Checkbox**, **Dialog**, **Sheet** (the drawer), **Tabs**, **Tooltip**, **Toast**
(Sonner), a lightweight **Form** wrapper (validation + per-field error display),
**DateRangePicker**, and **Pagination**. The exact set is pulled per screen, not all
up front.

### Reusable screen patterns

- **List + detail = drawer (Sheet).** The primary pattern for every list screen
  (Devices, Tariffs, Products, Loyalty, Audit, Clients): a full-width table whose row
  click slides a detail/edit panel in from the right over the still-visible list.
  Closing returns to the list. The **client card** uses the same pattern as a **wider
  drawer with internal tabs** (Кошелёк / История / Пакеты), keeping the cabinet
  uniform.
- **Tabbed page** — underline tabs for multi-tab areas (Монетизация, Отчёты, Зал и
  ПК, Настройки). Tabs are in-page, not top-level nav items.
- **Analytics pattern** — a visual "storefront" of KPI tiles + charts on top;
  drilling into a specific detailed report opens a tabular view with date-range /
  grouping / filter controls and a CSV export action. The two are two levels of one
  screen, not competing screens.
- **Data-region states** — explicit loading (skeletons), authoritative-empty, and
  error-with-retry for every data region, identical to the conventions established in
  sub-project 1. Never silent blanks or fabricated data.

### Money-operation safety

Every money operation and every irreversible action (wallet refund, manual ledger
correction, debt payment, device removal, owner-code rotation) goes through a
**confirmation dialog** that shows the explicit amount and/or reason. Outcomes are
**server-confirmed only** — no optimistic "success". Failures surface as localized,
actionable copy (consistent with the existing error-projection approach); success
surfaces as a toast.

## Screen Designs

### Зал и ПК (redesign of existing legacy screens)

Three tabs, rebuilt on the new Table/drawer/dialog primitives:

- **Карта зала** — zone/seat editor (add/rename/reorder/remove zones and seats),
  ETag-based optimistic-concurrency save with conflict reload, on the existing
  `getFloorMap` / `updateFloorMap` contracts.
- **Устройства** — device inventory table; row opens a drawer for rename, move-seat,
  and remove; online/offline + enrollment status badges; heartbeat freshness.
- **Ожидают подтверждения** — pending device-approval queue with approve/reject
  (reason captured); tab hidden when there are no pending devices.

### Настройки (redesign + extend)

- **Филиал** — branch profile (name, city) + device-approval setting toggle, on
  existing `getBranchProfile` / `updateBranchProfile` / `getBranchSettings` /
  `updateBranchSettings`.
- **Операторы и роли** — staff list + create operator, plus role change, profile
  edit, state (enable/disable), and password reset — all on contracts the backend
  already exposes (`PATCH .../roles`, `.../profile`, `.../state`,
  `.../password-reset`). Owner-only.

### Все филиалы (account-scoped)

A real aggregated overview dashboard: a card/row per branch showing that branch's
headline KPIs (devices online, active sessions, revenue today, attention count) and
a small trend, aggregated client-side from the existing per-branch dashboard-summary
and device endpoints. Plus the branch CRUD list (open/add/deactivate). Clicking a
branch switches the branch context and navigates into it.

### Монетизация (new, owner-only)

- **Тарифы** — tariff list + create/edit, including tariff **versions** (the backend
  models versioned tariffs), on `tariffs` contracts.
- **Товары** — POS catalog: categories and products CRUD, plus **stock levels /
  movements** (inventory), on `pos` catalog + `inventory` contracts.
- **Лояльность** — package definitions CRUD, on `packages` contracts.

### Клиенты / CRM (new)

Searchable player list (on `GET .../players` search). Row opens the client drawer
with internal tabs:

- **Кошелёк** — balance and the money actions: top-up, refund, manual correction,
  debt payment (each via a confirmation dialog), on
  `wallet-summary` / `wallet/top-ups` / `ledger/.../refunds` /
  `ledger/manual-corrections` / `debts/payments`.
- **История** — the player ledger entries.
- **Пакеты** — packages the player owns (on player-package contracts).

Player creation uses the existing `POST .../players`.

### Отчёты (new)

- **Аналитика** — the storefront: period control + KPI tiles + charts (revenue by
  day, breakdown by category, utilization), built on the dashboard-summary and the
  detailed report endpoints. Drilling into a report (Смены, Продажи, Игровое время,
  Касса, Действия операторов) opens the tabular view with date-range / grouping /
  filter and **CSV export** (the backend already serves `.../export.csv`).
- **Журнал событий** — the audit log search (filter, limit) on `GET .../audit`.

### Профиль и доступ (new)

Account profile and access for the signed-in owner/manager. Built on the identity
contracts available to the staff session; kept focused (no billing — that is
sub-project 3).

### Установка / Биллинг

- **Установка** — keep the working InstallScreen; light cosmetic pass to match the
  new look only.
- **Биллинг** — labelled "soon" placeholder; deferred to sub-project 3.

## Data and Contracts

The work is almost entirely frontend. `src/api/clubApi.ts` is extended with thin
wrapper methods over routes the backend already exposes: tariffs (+ versions),
packages, POS categories/products + catalog, inventory stock movements,
players/wallet-summary/ledger (top-ups, refunds, manual corrections, debts),
player packages, reports (shifts, sales, gameplay-time, cash-operations,
operator-actions) + their CSV exports, audit search, staff role/profile/state/
password-reset, and the aggregated branch summaries for "Все филиалы".

**No new backend contracts are planned.** If implementation reveals a genuine gap
(a value shown in a mock that no existing contract exposes), it is either derived
from available data or deferred behind a clearly-labelled placeholder — it must not
fabricate backend success. Any such gap is flagged explicitly, not silently
papered over.

## Error Handling

API errors surface as typed errors projected to localized, actionable copy
(loading → empty → error/retry), consistent with the existing error-projection
approach and with sub-project 1. No raw technical strings in the owner-facing path.
Money/destructive actions confirm first and report server-confirmed outcomes only.

## i18n

RU primary, EN secondary, extended to cover every new screen and string. Key parity
between `messages.ru` and `messages.en` is maintained (the project verifies parity).
Numbers/dates/currency formatted per locale; currency code stays backend-driven.

## Testing

Vitest + React Testing Library + jsdom (the project's existing setup), tests
colocated:

- View-model builders for each screen (pure transforms of contract DTOs).
- Each screen's loading / data / authoritative-empty / error-retry states.
- Role gating: `branch_manager` does not see owner-only areas (Монетизация,
  Настройки, Профиль), enforced in routing and render.
- Money operations: confirmation dialog gating, server-confirmed success (toast),
  and error projection — for top-up, refund, manual correction, debt payment.
- Drawer open/close and tab switching for the list+detail and tabbed-page patterns.
- Route resolution for all newly-routed `/club/*` paths inside the shell.
- Gates: `npm run build` (tsc + vite) and `npm test` green.

## Polish Backlog from Sub-Project 1 (closed here)

Carried forward from the sub-project 1 review and addressed as part of this work:
delete the now-orphaned `ClubDashboard` component; fix the Overview KPI
"devices online" denominator (currently uses `utilization.totalSeats` as a proxy);
replace positional `revenueBreakdown[0]/[1]` access; fix recharts tooltip raw slice
keys; replace the single-branch branch-switcher placeholder (`onSelectBranch` no-op)
with real branch switching.

## Implementation Decomposition

This brainstorm produces **one design spec** for the whole cabinet. Implementation
is split into a sequence of plans (each its own `writing-plans` document, executed
in order; each produces working, testable software on its own):

1. **Shared primitives + redesign of existing screens** — vendor the new UI
   primitives and harden the list+detail / tabbed-page patterns by redesigning the
   already-existing screens (Зал и ПК, Настройки, Операторы и роли) and building the
   "Все филиалы" aggregated dashboard. Also closes the sub-project 1 polish backlog.
2. **Монетизация** — Тарифы / Товары / Лояльность.
3. **Клиенты / CRM** — list + client drawer + money operations.
4. **Отчёты** (Аналитика + detailed reports + Журнал событий) **+ Профиль и доступ**.

Ordering rationale: build the reusable "furniture" first and prove it on existing
screens before the new high-value screens depend on it — the same foundation-first
approach that worked in sub-project 1.

## Out of Scope / Deferred

- Billing/subscription management (sub-project 3).
- All of `/admin/*` (sub-project 3).
- New backend contracts for any area (the existing surface is sufficient).
- Per-screen "all branches" aggregation beyond the "Все филиалы" overview.
- Webfont selection; marketing/landing site.

## Related

- `docs/superpowers/specs/2026-05-29-platform-web-design-foundation-and-club-overview-design.md`
  — sub-project 1 (foundation + Overview), the basis this builds on.
- `docs/superpowers/plans/2026-05-29-platform-web-foundation-app-shell-club-overview.md`
  — sub-project 1 plan.
- `docs/superpowers/plans/2026-05-24-afk4-club-self-service-onboarding.md` — the
  `/club/*` and `/admin/*` IA and backend contracts this consumes.
- `docs/superpowers/plans/2026-05-23-saas-control-plane-tenant-onboarding.md` — the
  `/admin/*` control-plane backend (sub-project 3).
- Reference benchmark: SmartShell.gg control panel feature set (aspirational
  completeness bar; AFK4 keeps live ops in the native Operator App).
