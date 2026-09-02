# Operator Unified-Admin Parity Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Закрыть обязательные Clients, Monetization, Settings и Venue пробелы Operator App, чтобы последующий под-проект мог удалить Platform.Web `/club` без потери функций.

**Architecture:** Каждая возможность встраивается в существующий Operator workspace и использует существующие backend endpoints, permissions и idempotency. Изменения идут независимыми вертикальными TDD-слайсами; после них повторный аудит формирует сертификат паритета. Platform.Web `/club` этим планом не удаляется.

**Tech Stack:** React 19, TypeScript, Bun test, Testing Library, Vite, `@afk4/i18n`, ASP.NET Core .NET 10 contracts.

## Global Constraints

- Не переносить старый Platform.Web UI; использовать текущие Operator `MgmtTable`/`MgmtDrawer`/`PanelModal` паттерны.
- Не добавлять device approval, pending approve/reject, `requireManualDeviceApproval` или drag-and-drop floor map.
- Валюта остаётся branch-level настройкой; entity currency picker не добавлять.
- UI permission-gate дублировать `hasPermission(nextBackend.session, ...)` перед mutation.
- Финансовые/package-команды ждут backend confirmation и используют новый idempotency key; ambiguous retry повторяет тот же key.
- Ошибка загрузки не превращается в пустые данные.
- Новые строки идут через `locales/{ru,en,tg}.json`; tg — настоящий таджикский.
- Каждый production-шаг начинается с падающего regression-теста.
- Не включать `.claude/memory/*` в task-коммиты.
- Не удалять `AFK4.Platform.Web /club`.

---

### Task 1: Multi-role сотрудники и self-deactivation

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/StaffRolesDestination.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/StaffRolesDestination.test.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Generated: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes `staffRoleOptions`, `staffRoleLabel`, `session.staffUserId` and existing staff API clients.
- Produces complete `roleNames: string[]` for invite/edit; empty role set is invalid.

- [ ] **Step 1: Write failing tests**

Select `branch_manager` and `technician`, then assert:

```tsx
expect(createStaffInvite).toHaveBeenCalledWith('b1', expect.objectContaining({
  roleNames: ['branch_manager', 'technician']
}));
expect(updateStaffUserRoles).toHaveBeenCalledWith('b1', staffUserId, {
  organizationId: 'org', roleNames: ['branch_manager', 'technician']
});
```

Add a fixture matching `backend.session.staffUserId` and assert no «Отключить доступ» action exists.

- [ ] **Step 2: Verify RED**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/destinations/StaffRolesDestination.test.tsx`

Expected: FAIL because only one role can be selected and self-deactivation is offered.

- [ ] **Step 3: Implement role-set state**

Replace scalar states with arrays and add:

```ts
export function toggleRole(current: string[], role: string): string[] {
  return current.includes(role)
    ? current.filter((candidate) => candidate !== role)
    : staffRoleOptions.filter((candidate) => [...current, role].includes(candidate));
}
```

Render labelled checkboxes for every `staffRoleOptions`, seed edit from the full server array, disable submit for `[]`, and send the full array. Omit disable actions when row `staffUserId === session?.staffUserId`.

- [ ] **Step 4: Generate i18n and verify GREEN**

```bash
cd packages/i18n && bun run gen && bun test
cd ../../src/AFK4.Operator.App.Web && bun test src/management/destinations/StaffRolesDestination.test.tsx
```

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/management/destinations/StaffRolesDestination* locales packages/i18n/src/messages.ts
git commit -m "fix(operator): сохранять полный набор ролей сотрудников"
```

---

### Task 2: Lifecycle клиента при inactive + debt

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/playersModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/players/playersModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDrawer.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDrawer.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`

**Interfaces:**
- Produces `PlayerClientItem.isActive: boolean`.
- Invariant: mutations use `isActive`; visual `status` never controls lifecycle.

- [ ] **Step 1: Write failing projection/drawer tests**

```ts
expect(projectPlayerClient(inactiveDebtor, t)).toMatchObject({
  isActive: false, debtMinorUnits: 3500, status: 'inactive'
});
```

Assert the drawer shows inactive and debt together, exposes activation, and hides money/package actions.

- [ ] **Step 2: Verify RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/operatorHelpers.test.ts src/players/playersModel.test.ts src/players/ClientDrawer.test.tsx
```

- [ ] **Step 3: Implement explicit lifecycle**

Add `isActive` to `PlayerClientItem` and project:

```ts
status: !isActive ? 'inactive' : debt > 0 ? 'debt' : 'active',
tone: !isActive ? 'regular' : debt > 0 ? 'debt' : 'active'
```

Use `client.isActive` for activation/deactivation and money/package visibility. Merge both `isActive` and projected status after mutation.

- [ ] **Step 4: Verify GREEN**

Run focused tests plus `ClientsTable.test.tsx` and `ClientActionsMenu.test.tsx`.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorHelpers* src/AFK4.Operator.App.Web/src/players src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx
git commit -m "fix(operator): разделить активность клиента и статус долга"
```

---

### Task 3: Time corrections, ledger detail и partial refund

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/CorrectionModal.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/CorrectionModal.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/LedgerRow.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/LedgerRow.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/RefundModal.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/RefundModal.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Modify/Generated: `locales/{ru,en,tg}.json`, `packages/i18n/src/messages.ts`

**Interfaces:**
- Produces `CorrectionAccount = 'wallet' | 'debt' | 'package_time' | 'bonus_time'`.
- `RefundModal` returns selected `minorUnits`; caller retains reason state.

- [ ] **Step 1: Write failing tests**

For 90 package minutes assert request fields `amount.minorUnits: 0` and `quantitySeconds: 5400`. For original refund 2000 assert 1250 is accepted and 2001 is rejected. Assert `LedgerRow` renders account label and `90 мин`.

- [ ] **Step 2: Verify RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/players/CorrectionModal.test.tsx src/players/RefundModal.test.tsx src/players/LedgerRow.test.tsx
```

- [ ] **Step 3: Implement account-aware requests**

```ts
const isTime = account === 'package_time' || account === 'bonus_time';
const request = {
  organizationId,
  accountType: account,
  amount: { currencyCode, minorUnits: isTime ? 0 : signedMinorUnits },
  quantitySeconds: isTime ? signedWholeMinutes * 60 : 0,
  reason,
  idempotencyKey: createIdempotencyKey('manual-correction')
};
```

Make refund amount editable and constrained to `0 < amount <= abs(original)`. Render ledger `accountType` and nonzero minutes.

- [ ] **Step 4: Generate i18n and verify GREEN**

Run i18n generation/tests and the three focused suites.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx locales packages/i18n/src/messages.ts
git commit -m "feat(operator): закрыть корректировки времени и частичный возврат"
```

---

### Task 4: Пакеты клиента и wallet-backed продажа из Кассы

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/PackagesSection.tsx`
- Create: `src/AFK4.Operator.App.Web/src/players/PackagesSection.test.tsx`
- Create: `src/AFK4.Operator.App.Web/src/PackagePurchasePanel.tsx`
- Create: `src/AFK4.Operator.App.Web/src/PackagePurchasePanel.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDrawer.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`
- Modify/Generated: `locales/{ru,en,tg}.json`, `packages/i18n/src/messages.ts`

**Interfaces:**
- `PackagesSection({ packages, loading, errorDetail })` is read-only.
- `PackagePurchasePanel({ backend, player, options, shiftOpen, onPurchased })` calls wallet-backed `purchasePackage`; it never creates a POS cash settlement and disables purchase when `shiftOpen === false`.

- [ ] **Step 1: Write failing component tests**

Assert package name, included/bonus remaining minutes, expiry and active state. Confirm purchase calls:

```ts
purchasePackage(player.playerAccountId, {
  organizationId: 'org',
  packageDefinitionId: '11111111-1111-1111-1111-111111111111',
  idempotencyKey: expect.any(String)
});
```

Assert no purchase without `packages.purchase` or active player.

- [ ] **Step 2: Verify RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/players/PackagesSection.test.tsx src/PackagePurchasePanel.test.tsx src/BackendPosWorkspace.test.tsx
```

- [ ] **Step 3: Implement package read path**

Load `getPlayerPackages(playerId)` with independent loading/error state when a client is selected. Render `PackagesSection`; never convert load error to `[]`.

- [ ] **Step 4: Implement Cash purchase panel**

Load package options for selected client. Mount the panel below the client row, separate from goods cart. Reuse one `attemptKeyRef` after ambiguous transport failure and clear it only on authoritative success or definite 4xx. On success reload wallet/player packages.

- [ ] **Step 5: Generate i18n and verify GREEN**

Run i18n, new components and `BackendPosWorkspace.test.tsx`.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players src/AFK4.Operator.App.Web/src/PackagePurchasePanel* src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPosWorkspace* locales packages/i18n/src/messages.ts
git commit -m "feat(operator): продать пакет клиенту из Кассы"
```

---

### Task 5: Переиспользование категорий товара

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/goods/categoryModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/goods/categoryModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/GoodsDestination.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/GoodsDestination.test.tsx`
- Modify/Generated: `locales/{ru,en,tg}.json`, `packages/i18n/src/messages.ts`

**Interfaces:**
- `CategoryOption { categoryId: string; label: string }`.
- `deriveCategoryOptions(products, sessionCategories, unknownPrefix): CategoryOption[]`.

- [ ] **Step 1: Write failing model/form tests**

Assert category dedupe and fallback `Категория bbbbbbbb`. Test existing-category create does not call `createProductCategory`; edit sends the selected new `categoryId`.

- [ ] **Step 2: Verify RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/management/destinations/goods/categoryModel.test.ts src/management/destinations/GoodsDestination.test.tsx
```

- [ ] **Step 3: Implement category modes**

Derive labels from `categoryName`, otherwise short-id fallback. Create modal offers existing/new modes and calls `createProductCategory` only in new mode. Edit drawer shows a category select and sends it in PATCH. Show category in table/meta.

- [ ] **Step 4: Generate i18n and verify GREEN**

Run i18n and focused suites.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/management/destinations/GoodsDestination* src/AFK4.Operator.App.Web/src/management/destinations/goods locales packages/i18n/src/messages.ts
git commit -m "fix(operator): переиспользовать категории товаров"
```

---

### Task 6: Честные monetization loading/error states

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/management/ManagementWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/ManagementWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/TariffsPackagesDestination.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/TariffsPackagesDestination.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/types.ts`

**Interfaces:**
- Produces `ResourceState<T> = { status: LoadStatus; data: T; errorDetail?: string }` for package options.

- [ ] **Step 1: Write failing failure-is-not-empty test**

Reject `getPackageOptions` while other settings loads pass. Assert Tariffs remains usable, Packages shows error/Retry, and empty-package copy is absent.

- [ ] **Step 2: Verify RED**

Run `bun test` for `ManagementWorkspace.test.tsx` and `TariffsPackagesDestination.test.tsx`.

- [ ] **Step 3: Implement independent package resource state**

Remove `.catch(() => [])`. Load packages separately, preserve last successful data during retry, and pass `packageState` plus `onRetryPackages`. Render error only in Packages destination.

- [ ] **Step 4: Verify GREEN and commit**

```bash
cd src/AFK4.Operator.App.Web && bun test src/management/ManagementWorkspace.test.tsx src/management/destinations/TariffsPackagesDestination.test.tsx
git add src/AFK4.Operator.App.Web/src/management
git commit -m "fix(operator): не маскировать ошибку загрузки пакетов"
```

---

### Task 7: Rename/remove lifecycle устройства

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/devices.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/halls/DevicesTab.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/halls/DevicesTab.test.tsx`

**Interfaces:**
- `RenameDeviceRequest { organizationId: Guid; displayName: string }`.
- `RemoveDeviceRequest { organizationId: Guid; reason: string }`.
- Rename permission: `devices.seat_assignment.assign`; remove: `devices.credentials.revoke`.

- [ ] **Step 1: Write failing route/UI tests**

Call `renameDevice` and `removeDevice` with exact bodies. UI tests assert trimmed rename and reason+confirmation before removal.

- [ ] **Step 2: Verify RED**

```bash
cd src/AFK4.Operator.App.Web
bun test src/operatorApiClients.test.ts src/management/destinations/halls/DevicesTab.test.tsx
```

- [ ] **Step 3: Implement typed API and drawer controls**

Add POST `/rename` and `/remove`. Recheck permissions inside handlers, require nonempty trimmed values, await backend, reload inventory, and close removed-device drawer.

- [ ] **Step 4: Verify GREEN/build and commit**

```bash
cd src/AFK4.Operator.App.Web && bun test src/operatorApiClients.test.ts src/management/destinations/halls/DevicesTab.test.tsx && bun run build
git add src/AFK4.Operator.App.Web/src/api/clients/devices.ts src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts src/AFK4.Operator.App.Web/src/management/destinations/halls/DevicesTab*
git commit -m "feat(operator): закрыть lifecycle управления устройством"
```

---

### Task 8: App integration regression gate

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`

**Interfaces:**
- Verifies Tasks 1–7 through real app wiring and HTTP mock paths/bodies.

- [ ] **Step 1: Add integration scenarios**

Add tests for multi-role payload, inactive debtor activation, package-time correction, Cash package purchase, category update, package load error, and device rename/remove. Assert final HTTP path and body.

- [ ] **Step 2: Verify RED**

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx`

- [ ] **Step 3: Update only missing mock behavior**

Extend `devMockBackend` only for rename/remove and authoritative package refresh. Do not introduce alternate production paths.

- [ ] **Step 4: Run full frontend gates**

```bash
cd src/AFK4.Operator.App.Web
bun test src/App.test.tsx
bun test
bun run build
cd ../../../packages/i18n && bun test
```

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.test.tsx src/AFK4.Operator.App.Web/src/devMockBackend.ts
git commit -m "test(operator): закрепить полный паритет club-потоков"
```

---

### Task 9: Повторный аудит и сертификат

**Files:**
- Create: `docs/superpowers/notes/2026-07-28-operator-parity-certificate.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`

**Interfaces:**
- Produces GO/NO-GO для отдельного №4; `/club` не удаляет.

- [ ] **Step 1: Repeat four-domain audit**

Для каждой возможности `/club/{clients,monetization,settings,venue}` записать Operator location, permission, test и outcome (`covered`, `operator wider`, `approved descope`). Любой uncovered non-descope даёт NO-GO.

- [ ] **Step 2: Write certificate**

Include exact verdict, approved descope, fresh focused/full/App/i18n/build evidence. Не писать GO без полного покрытия и зелёных гейтов.

- [ ] **Step 3: Update compact progress**

Добавить один Implemented bullet, свежие counts в Latest Verification и оставить `/club` removal в Recommended Next Work.

- [ ] **Step 4: Run final checks**

```bash
git diff --check
cd src/AFK4.Operator.App.Web && bun test && bun run build
cd ../../../packages/i18n && bun test
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/notes/2026-07-28-operator-parity-certificate.md docs/progress/2026-05-12-vertical-slice-progress.md
git commit -m "docs(operator): сертифицировать паритет с Platform.Web club"
```

---

## Self-Review

### Spec coverage

- Staff multi-role/self-action: Task 1.
- inactive+debt: Task 2.
- corrections/ledger/refund: Task 3.
- package list/purchase: Task 4.
- category reuse/change: Task 5.
- honest load errors: Task 6.
- device rename/remove: Task 7.
- cross-workspace wiring: Task 8.
- certificate/progress: Task 9.
- Approved descope remains excluded; `/club` is not deleted.

### Type consistency

- `PlayerClientItem.isActive` lands before later client/package tasks consume it.
- Package purchase is wallet-backed and separate from POS settlement.
- Category options use one `{ categoryId, label }` shape.
- Device request fields match Shared Contracts JSON names.
- Load failure remains distinct from successful empty data.

### Verification boundary

Tasks change Operator Web, locale sources/generated catalog and docs. Existing backend contracts are reused. Full solution verification is required at branch finishing before push/merge, not inside each frontend TDD slice.
