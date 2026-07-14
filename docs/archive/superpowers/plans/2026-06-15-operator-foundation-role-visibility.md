# Operator Foundation — Role → Sections Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lock the Этап-0 §2 contract "role → visible rail sections" with a guard test, and reconcile the real backend role→permission map + frontend workspace→permission rules so actual visibility matches the contract.

**Architecture:** Section visibility is `f(role→permissions [backend PermissionCatalog.cs], workspace→permissions [frontend workspacePermissionRules], section grouping [frontend navSections])`. We (1) add a frontend guard test encoding the §2 table, (2) tighten frontend workspace rules so sections gate on *action/manage* permissions instead of passive *view* permissions (kills the cashier→Управление and technician→Касса leaks), (3) reconcile the backend `shift_supervisor` permission set per user decisions, (4) amend the spec row for the auditor and update project memory.

**Tech Stack:** React 19 + bun test (frontend), C# / xUnit (backend), vanilla TS.

**Locked decisions (this session):**
- `shift_supervisor`: **+** `billing.money_action.approve` (gets Касса approval), **−** `audit.view`, **−** `diagnostics.view` (no Управление).
- `accountant_auditor`: keep `players.view` / `billing.view` / `reservations.view` (Клиенты & Брони stay **read-only visible**) → **amend the spec table**, not the backend.
- Leaks caused by passive view-perms (cashier→Управление via `tariffs.view`, technician→Касса via `inventory.view`) → fix on the **frontend** by tightening `workspacePermissionRules` (no capability change).

**Final target matrix (rail section keys: `map`=Карта, `cashier`=Касса, `booking`=Брони, `players`=Клиенты, `reports`=Отчёты, `admin`=Управление):**

| Role | map | cashier | booking | players | reports | admin |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| `cashier_operator` | ✅ | ✅ | ✅ | ✅ | — | — |
| `shift_supervisor` | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| `branch_manager` | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| `technician` | ✅ | — | — | — | — | ✅ |
| `accountant_auditor` | — | ✅ | ✅ | ✅ | ✅ | ✅ |

---

## File Structure

- `src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts` — **new** frontend guard test (role → visible sections, §2 contract). Authoritative visibility guard.
- `src/AFK4.Operator.App.Web/src/operatorPermissions.ts` — **modify** `workspacePermissionRules` (`pos`, `shop_orders`, `settings`).
- `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` — **modify** the `ShiftSupervisor` permission set.
- `tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogContractTests.cs` — **new** backend guard test (supervisor reconciliation invariants).
- `docs/superpowers/specs/2026-06-14-operator-foundation-design.md` — **modify** §2 auditor row + reconciliation note.
- `.claude/memory/operator-redesign-phase0-decisions.md` — **modify** resume point.

---

## Task 1: Frontend guard test for role → visible sections (RED)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { navSections } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import type { OperatorAuthSession } from './authClient';

// Canonical role → permission map. MUST mirror PermissionCatalog.cs (backend source of truth)
// and the §2 visibility contract in docs/superpowers/specs/2026-06-14-operator-foundation-design.md.
// Changing a role's permissions on the backend REQUIRES updating this fixture and the expected
// sections below in the same change. (#37 honest, trackable contract — no silent drift.)
const rolePermissions: Record<string, string[]> = {
  cashier_operator: [
    'floor_map.view', 'sessions.start', 'sessions.extend', 'sessions.transfer', 'sessions.end',
    'sessions.view', 'players.create', 'players.view', 'billing.view', 'billing.wallet.top_up',
    'billing.debt.pay', 'tariffs.view', 'packages.view', 'packages.purchase', 'shifts.open',
    'shifts.view', 'reservations.view', 'reservations.manage', 'pos.sales.create', 'pos.sales.pay',
    'receipts.view'
  ],
  shift_supervisor: [
    'devices.commands.status.view', 'devices.detail.view', 'floor_map.view', 'sessions.start',
    'sessions.extend', 'sessions.transfer', 'sessions.end', 'sessions.view', 'players.create',
    'players.view', 'billing.view', 'billing.wallet.top_up', 'billing.refund',
    'billing.manual_correction', 'billing.debt.pay', 'billing.money_action.approve', 'tariffs.view',
    'packages.view', 'packages.purchase', 'shifts.open', 'shifts.close', 'shifts.view',
    'shifts.cash.manage', 'reports.view', 'reservations.view', 'reservations.manage',
    'pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void', 'inventory.view',
    'receipts.view', 'updates.status.view'
  ],
  branch_manager: [
    'devices.enrollment_codes.create', 'devices.commands.dispatch', 'devices.commands.status.view',
    'devices.credentials.rotate', 'devices.credentials.revoke', 'devices.seat_assignment.assign',
    'devices.detail.view', 'devices.install', 'floor_map.view', 'layout.manage', 'sessions.start',
    'sessions.extend', 'sessions.transfer', 'sessions.end', 'sessions.view', 'players.create',
    'players.view', 'billing.view', 'billing.wallet.top_up', 'billing.refund',
    'billing.manual_correction', 'billing.debt.pay', 'billing.money_action.approve', 'tariffs.manage',
    'tariffs.view', 'packages.manage', 'packages.view', 'packages.purchase', 'shifts.open',
    'shifts.close', 'shifts.view', 'shifts.cash.manage', 'reports.view', 'reservations.view',
    'reservations.manage', 'pos.catalog.manage', 'shop.orders.manage', 'pos.sales.create',
    'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void', 'inventory.stock.manage', 'inventory.view',
    'receipts.view', 'updates.packages.manage', 'updates.rollouts.manage', 'updates.status.view',
    'diagnostics.view', 'identity.branch_staff.manage', 'audit.view', 'branches.settings.manage'
  ],
  technician: [
    'devices.enrollment_codes.create', 'devices.commands.dispatch', 'devices.commands.status.view',
    'devices.credentials.rotate', 'devices.credentials.revoke', 'devices.seat_assignment.assign',
    'devices.detail.view', 'devices.install', 'floor_map.view', 'inventory.view',
    'updates.packages.manage', 'updates.rollouts.manage', 'updates.status.view', 'diagnostics.view'
  ],
  accountant_auditor: [
    'sessions.view', 'players.view', 'billing.view', 'tariffs.view', 'packages.view', 'shifts.view',
    'reports.view', 'reservations.view', 'inventory.view', 'receipts.view', 'updates.status.view',
    'diagnostics.view', 'audit.view'
  ]
};

// §2 contract: which rail sections (navSections[].key) each role may see.
const expectedSections: Record<string, string[]> = {
  cashier_operator: ['map', 'booking', 'players', 'cashier'],
  shift_supervisor: ['map', 'booking', 'players', 'cashier', 'reports'],
  branch_manager: ['map', 'booking', 'players', 'cashier', 'reports', 'admin'],
  technician: ['map', 'admin'],
  accountant_auditor: ['booking', 'players', 'cashier', 'reports', 'admin']
};

function visibleSections(permissions: string[]): string[] {
  const session = { permissions } as OperatorAuthSession;
  return navSections
    .filter((section) => section.items.some((item) => canOpenWorkspace(session, item.id)))
    .map((section) => section.key)
    .sort();
}

describe('role → visible rail sections (Этап 0 §2 contract)', () => {
  for (const role of Object.keys(expectedSections)) {
    it(`${role} sees exactly the contracted sections`, () => {
      expect(visibleSections(rolePermissions[role])).toEqual([...expectedSections[role]].sort());
    });
  }
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/operatorVisibility.test.ts`
Expected: FAIL — `cashier_operator` and `shift_supervisor` get extra `admin`, `technician` gets extra `cashier` (current `workspacePermissionRules` leak sections via passive view-perms).

- [ ] **Step 3: Commit the failing test**

```bash
git add src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts
git commit -m "test(operator): guard role → visible rail sections per Этап-0 §2"
```

---

## Task 2: Tighten workspace permission rules (GREEN)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts:60-109` (`workspacePermissionRules`)

- [ ] **Step 1: Tighten `pos`, `shop_orders`, `settings` rules**

In `workspacePermissionRules`, replace the `pos`, `shop_orders`, and `settings` entries so sections gate on *action/manage* permissions, not passive *view* permissions.

Replace the `pos` entry (currently includes `viewInventory`, `viewShift`, `viewReports`):

```ts
  pos: [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale
  ],
```

Replace the `shop_orders` entry (currently includes `viewInventory`):

```ts
  shop_orders: [permissionNames.createPosSale],
```

Replace the `settings` entry (drop passive view-perms `viewDeviceDetail`, `viewInventory`, `viewDiagnostics`, `viewUpdateStatus`, `viewTariffs`; keep manage/admin perms):

```ts
  settings: [
    permissionNames.manageBranchStaff,
    permissionNames.manageLayout,
    permissionNames.createDeviceEnrollmentCode,
    permissionNames.assignDeviceSeat,
    permissionNames.rotateDeviceCredential,
    permissionNames.revokeDeviceCredential,
    permissionNames.manageInventoryStock,
    permissionNames.managePosCatalog,
    permissionNames.managePackages,
    permissionNames.manageUpdatePackages,
    permissionNames.manageUpdateRollouts,
    permissionNames.manageTariffs
  ],
```

Leave `map`, `dashboard`, `booking`, `players`, `payments`, `payment_cards`, `logs`, `review`, `loyalty`, `news`, `shifts` unchanged.

- [ ] **Step 2: Run the guard test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/operatorVisibility.test.ts`
Expected: PASS — all 5 roles match the §2 table.

- [ ] **Step 3: Run the full Operator suite (no regressions)**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test`
Expected: PASS. If `App.test.tsx` permission-gating cases fail, inspect them — a real behavioral change to nav surfacing must be reconciled against the §2 contract (fix the test to the contract, do not loosen the rule back).

- [ ] **Step 4: Build**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: build succeeds, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorPermissions.ts
git commit -m "fix(operator): gate rail sections on action/manage perms, not passive view"
```

---

## Task 3: Reconcile backend shift_supervisor permissions + guard test

**Files:**
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs:123-159` (`ShiftSupervisor` set)
- Create: `tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogContractTests.cs`

- [ ] **Step 1: Write the failing backend test**

Create `tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogContractTests.cs`:

```csharp
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

// Guards the Этап-0 §2 visibility contract on the backend side: the shift_supervisor
// reconciliation (approve money actions; no audit/diagnostics → no Управление).
// Mirror of the frontend operatorVisibility.test.ts fixture.
public sealed class PermissionCatalogContractTests
{
    [Fact]
    public void ShiftSupervisor_CanApproveMoneyActions()
    {
        var permissions = PermissionCatalog.GetPermissions([StaffRoleNames.ShiftSupervisor]);
        Assert.Contains(StaffPermissionNames.ApproveMoneyAction, permissions);
    }

    [Theory]
    [InlineData(StaffPermissionNames.ViewAudit)]
    [InlineData(StaffPermissionNames.ViewDiagnostics)]
    public void ShiftSupervisor_HasNoManagementOnlyVisibility(string permission)
    {
        var permissions = PermissionCatalog.GetPermissions([StaffRoleNames.ShiftSupervisor]);
        Assert.DoesNotContain(permission, permissions);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PermissionCatalogContractTests`
Expected: FAIL — supervisor currently lacks `ApproveMoneyAction` and still has `ViewAudit` + `ViewDiagnostics`.

- [ ] **Step 3: Edit the ShiftSupervisor permission set**

In `PermissionCatalog.cs`, inside the `[StaffRoleNames.ShiftSupervisor]` `HashSet<string>`:
- **Add** `StaffPermissionNames.ApproveMoneyAction,` (place it right after `StaffPermissionNames.PayDebt,` to mirror the manager block ordering).
- **Remove** the line `StaffPermissionNames.ViewAudit,` (currently the last entry of the set).
- **Remove** the line `StaffPermissionNames.ViewDiagnostics,`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PermissionCatalogContractTests`
Expected: PASS.

- [ ] **Step 5: Run the broader identity test set (no regressions)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter Identity`
Expected: PASS. If a test asserted supervisor had audit/diagnostics, reconcile it to the new contract.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/PermissionCatalog.cs tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogContractTests.cs
git commit -m "feat(identity): shift_supervisor approves money actions, drops audit/diagnostics"
```

---

## Task 4: Amend spec §2 + update memory

**Files:**
- Modify: `docs/superpowers/specs/2026-06-14-operator-foundation-design.md:83` (auditor row) + the honesty note
- Modify: `.claude/memory/operator-redesign-phase0-decisions.md`

- [ ] **Step 1: Amend the auditor row in the §2 table**

In the §2 table, replace the `accountant_auditor` row so Брони and Клиенты are read-visible:

```markdown
| `accountant_auditor` (бухгалтер/аудитор) | — | ✅ (платежи/возвраты, чтение) | ✅ (чтение) | ✅ (чтение) | ✅ | ✅ (только аудит/логи) |
```

- [ ] **Step 2: Record the reconciliation outcome under the honesty note**

Immediately after the existing `> **Честная оговорка (#38):** …` blockquote, add:

```markdown
> **Реконсиляция (выполнена 2026-06-15):** сверка с `PermissionCatalog.cs` дала расхождения, устранены так:
> `shift_supervisor` получил `billing.money_action.approve` и потерял `audit.view`/`diagnostics.view`
> (Касса с одобрением, без Управления); `accountant_auditor` сохраняет read-доступ к Клиентам/Броням
> (таблица выше поправлена под это); протечки секций от пассивных view-прав (кассир→Управление,
> техник→Касса) убраны ужатием `workspacePermissionRules` на фронте. Контракт стережёт
> `operatorVisibility.test.ts` (фронт) + `PermissionCatalogContractTests.cs` (бэкенд).
```

- [ ] **Step 3: Update the resume-point memory**

In `.claude/memory/operator-redesign-phase0-decisions.md`, mark the "Видимость роль→секции" piece as DONE with a one-line summary of the reconciliation, and note the next remaining pieces are «Примитивы» and «Shell-каркас».

- [ ] **Step 4: Commit**

```bash
git add docs/superpowers/specs/2026-06-14-operator-foundation-design.md .claude/memory/operator-redesign-phase0-decisions.md
git commit -m "docs(operator): reconcile §2 visibility contract + resume memory"
```

---

## Final verification

- [ ] **Frontend gates:** `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test && bun run build` → all green.
- [ ] **Backend gate:** `dotnet test tests/AFK4.Platform.Api.Tests --filter Identity` → green.
- [ ] **Contract honored:** `operatorVisibility.test.ts` matches the final target matrix above for all 5 roles.

---

## Self-Review notes

- **Spec coverage:** §2 contract (guard test) ✅ Task 1/2; backend reconciliation (#37 "расхождения устранены, не замаскированы") ✅ Task 3; spec amendment for the auditor decision ✅ Task 4.
- **Drift risk (known, accepted):** the frontend `rolePermissions` fixture hand-mirrors backend `PermissionCatalog.cs` (no cross-language fixture infra exists in this monorepo — frontend already mirrors `permissionNames` by hand). Both the fixture and `PermissionCatalogContractTests.cs` are pinned to §2 and cross-referenced in comments; the backend test fails if the catalog drifts off the reconciled supervisor contract. A shared JSON contract consumed by both sides is deliberately **not** built now (YAGNI, #19) — revisit only if drift bites.
- **Type consistency:** `navSections[].items[].id` is `WorkspaceId`; `canOpenWorkspace(session, workspaceId)` takes `WorkspaceId`; section keys used in `expectedSections` (`map`/`booking`/`players`/`cashier`/`reports`/`admin`) match `navSections[].key` exactly.
</content>
</invoke>
