---
name: operator-redesign-phase0-decisions
description: Программа этапов Operator-редизайна + durable-факты (валюта, App.test, design-system)
metadata: 
  node_type: memory
  type: project
  originSessionId: 8ca9965a-38e4-43de-a075-003f1af1b3c8
---

Полный овэрхол Operator.App разбит на **«Этапы»**: 0 Фундамент → 1 Карта → 2 Касса → 3 Клиенты → 4 Брони → 5 Отчёты → 6 Управление (каждый: спек→план→PR).

**Сделано:** Этап 0 «Фундамент» ЗАКРЫТ; Этап 1 «Карта» ЗАКРЫТ (карта = только грид, см. [[afk4-operator-map-epic]]); Этап 4 «Брони» ЗАКРЫТ (PR #99, см. [[afk4-online-booking-autoconfirm-epic]]); Этап 3 «Клиенты» ЗАКРЫТ (PR #103–#108, см. [[afk4-operator-clients-epic]]). **Этап 2 «Касса» В РАБОТЕ: S0 каркас (PR #109) + S1 смена (PR #110) в main. Впереди: S2/S3 Кассы, затем 5 Отчёты, 6 Управление.**

**Касса (Этап 2) — состояние:** канон = spec `docs/superpowers/specs/2026-06-24-operator-cash-design.md`. Раздел = единый воркспейс `cash` = шапка-якорь `CashShiftHeader` (статус смены + командная панель Открыть/Внести/Изъять/Закрыть → PanelModal-модалки, оркестрация `CashShiftCommandBar`) + под-вкладки. **S0** (PR #109): каркас, 5 воркспейсов перенесены 1:1. **S1** (PR #110): жизненный цикл смены вынесен из «Платежей» в шапку; вкладка **«Смена»** (`CashShiftWorkspace` = кокпит выручка/сверка/движения/CSV-экспорт/история из `shiftRevenue`); вкладки `payments`+`shifts` слиты в `shift`; `BackendPaymentsWorkspace`+`ShiftsWorkspace` УДАЛЕНЫ; рефреш шапки+вкладки через `shiftNonce`. Текущие вкладки Кассы: `sales`(POS)/`orders`/`shift`/`review`. **Осталось: S2** (приходно-расходный ордер UI + полная лента операций/сетка методов в «Журнал кассы» + X/Z-отчёты read-only из revenue) **+ S3** (слияние Продажи+Заказы). **ДОЛГ S2:** вычистить осиротевшие `op.payments.*` (~98 ключей, мертвы после удаления воркспейса; guard зелёный, безвредны) — «Журнал кассы» реорганизует i18n.

**Бэк (готов, money-path не трогаем):** `ShiftEndpoints` (open/current/cash-movements/close — close=полный пересчёт expected vs counted + tolerance + manager sign-off; revenue/current+revenue), `PosEndpoints`, `MoneyActionEndpoints` (refund/correction approval), `ReportEndpoints`+CSV. **Нет на бэке:** X/Z как сущности, инкассация с банком (= cash_out), детализация по номиналам. `shiftRevenue.current` отдаёт shiftId+state+earned+inflow+cash{starting/expected/counted/difference} — хватает шапке и кокпиту.

Durable-факты:
- **Деньги**: `currencySymbol()` — const-мапа в `@afk4/money` (НЕ i18n), TJS→«с.»; единый форматтер.
- **App.test** гонять ОТДЕЛЬНЫМ `bun test`-вызовом (утечка `mock.module` process-wide) — НЕ сливать обратно в общий прогон (см. [[frontends-on-bun-test]]).
- Design-system = канон существующих токенов + примитивы Toast/Skeleton/EmptyState; извлекать по ходу.
- Процессный урок: не доводить пиксели в HTML-мокапах.
- **Деньги парсинг (S1):** `parseMoneyInputMinorUnits` отвергает 0 (строго >0) — для счёта/факта где 0 валиден (закрытие смены с пустой кассой) брать `parseNonNegativeMoneyInputMinorUnits`. `formatMoney(TJS)` рендерит целые БЕЗ дробной части: `115 с.` (не `115,00 TJS`) — тесты ассертить под реальный вывод; `formatMoney(null)` → `0 с.` (гардить null→placeholder в денежных строках, иначе ложно «сошлось»).
- **App.test флак-урок (S1):** действие, дёргающее рефреш через nonce-бамп (`onShiftChanged → shiftNonce++ → перезапуск load-эффектов`), порождает «вторую волну» фетчей; тест обязан её дренировать (await пост-действенного рефетча, напр. счётчик фетчей `>= before+2`), иначе повисшая async утекает в соседние тесты → перемежающийся флак (таймауты, «Unhandled: Unable to find heading»). Контроллерская многопрогонная верификация (App.test 6×) ловит то, что одиночный прогон имплементера пропускает.
- **Тест-паттерн боевого клиента:** компоненты, строящие `createAuthenticatedOperatorClients`, должны принимать инъект-клиент и строить боевой только при `backend && !injected` (фейк-backend в тестах с `config:'x'` бросает «Invalid URL» в `PlatformApiClient` на init); либо строить лениво в обработчике, не на рендере.
