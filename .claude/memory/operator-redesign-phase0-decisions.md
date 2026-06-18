---
name: operator-redesign-phase0-decisions
description: Программа этапов Operator-редизайна + durable-факты (валюта, App.test, design-system)
metadata: 
  node_type: memory
  type: project
  originSessionId: 8ca9965a-38e4-43de-a075-003f1af1b3c8
---

Полный овэрхол Operator.App разбит на **«Этапы»**: 0 Фундамент → 1 Карта → 2 Касса → 3 Клиенты → 4 Брони → 5 Отчёты → 6 Управление (каждый: спек→план→PR).

**Сделано:** Этап 0 «Фундамент» ЗАКРЫТ; Этап 1 «Карта» ЗАКРЫТ (карта = только грид, см. [[afk4-operator-map-epic]]); Этап 4 «Брони» ЗАКРЫТ (PR #99, см. [[afk4-online-booking-autoconfirm-epic]]). **Впереди: Этапы 2 Касса, 3 Клиенты, 5 Отчёты, 6 Управление.**

Durable-факты:
- **Деньги**: `currencySymbol()` — const-мапа в `@afk4/money` (НЕ i18n), TJS→«с.»; единый форматтер.
- **App.test** гонять ОТДЕЛЬНЫМ `bun test`-вызовом (утечка `mock.module` process-wide) — НЕ сливать обратно в общий прогон (см. [[frontends-on-bun-test]]).
- Design-system = канон существующих токенов + примитивы Toast/Skeleton/EmptyState; извлекать по ходу.
- Процессный урок: не доводить пиксели в HTML-мокапах.
