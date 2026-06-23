---
name: operator-redesign-phase0-decisions
description: Программа этапов Operator-редизайна + durable-факты (валюта, App.test, design-system)
metadata: 
  node_type: memory
  type: project
  originSessionId: 8ca9965a-38e4-43de-a075-003f1af1b3c8
---

Полный овэрхол Operator.App разбит на **«Этапы»**: 0 Фундамент → 1 Карта → 2 Касса → 3 Клиенты → 4 Брони → 5 Отчёты → 6 Управление (каждый: спек→план→PR).

**Сделано:** Этап 0 «Фундамент» ЗАКРЫТ; Этап 1 «Карта» ЗАКРЫТ (карта = только грид, см. [[afk4-operator-map-epic]]); Этап 4 «Брони» ЗАКРЫТ (PR #99, см. [[afk4-online-booking-autoconfirm-epic]]); Этап 3 «Клиенты» ЗАКРЫТ (PR #103–#108 в main, см. [[afk4-operator-clients-epic]]). **Сейчас в работе: Этап 2 «Касса». Впереди после неё: 5 Отчёты, 6 Управление.**

**Касса (Этап 2) — стартовые факты разведки (2026-06-23):** единого раздела «Касса» на фронте НЕТ — функции распылены по воркспейсам `pos` (`BackendPosWorkspace.tsx`, ~1111 строк, полноценный POS: каталог/корзина/оплата cash·card·deposit/чеки), `payments` (`BackendPaymentsWorkspace.tsx` — тут спрятаны операции смены: openShift/recordCashMovement/closeShift + «Сверка кассы»), `shifts` (`ShiftsWorkspace.tsx`, ~108 строк, ТОЛЬКО просмотр выручки/истории), `review` (money-actions аппрув). Nav-группа `cashier` = «Касса» уже есть. **Бэк богатый и почти готовый:** `ShiftEndpoints` (open/current/cash-movements/close/revenue — close делает полный пересчёт expected vs counted + tolerance + manager sign-off), `PosEndpoints` (sales/payments/refunds/void/catalog/inventory), `MoneyActionEndpoints` (refund/correction approval-очередь), `ReportEndpoints` (shifts/sales/cash-operations + CSV). Сервисы: `EfShiftService`, `EfPosService`, `MoneyActionApprovalService`, `MoneyControlPolicy`/`StaffMoneyCapPolicy`. **Нет на бэке:** X/Z-отчётов как операций, инкассации с адресом/банком (моделируется как cash_out), денежной детализации по номиналам. i18n: `op.pos.*` 151 ключ, `op.shifts.*` 17, `op.cash*`/`op.register*` — 0.

Durable-факты:
- **Деньги**: `currencySymbol()` — const-мапа в `@afk4/money` (НЕ i18n), TJS→«с.»; единый форматтер.
- **App.test** гонять ОТДЕЛЬНЫМ `bun test`-вызовом (утечка `mock.module` process-wide) — НЕ сливать обратно в общий прогон (см. [[frontends-on-bun-test]]).
- Design-system = канон существующих токенов + примитивы Toast/Skeleton/EmptyState; извлекать по ходу.
- Процессный урок: не доводить пиксели в HTML-мокапах.
