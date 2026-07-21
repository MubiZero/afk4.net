---
name: operator-laggard-screens-kit-migration
description: "Kit-миграция 4 отставших экранов Управления (Клуб/Лояльность/Шлюзы/Новости) — код готов, ждёт визуальной приёмки."
metadata: 
  node_type: memory
  type: project
  originSessionId: 5821be94-92d6-4025-9110-d29b28e7c3be
  modified: 2026-07-20T10:43:18.640Z
---

Четыре «отставших» экрана раздела «Управление» оператора переведены на общий дизайн-kit
(были на легаси-разметке мимо `MgmtTable`/`MgmtDrawer`/`mgmt-form`/`ui-chip`/`EmptyState`/
`CriticalActionConfirmation`). Спека `docs/superpowers/specs/2026-07-20-operator-management-laggard-screens-kit-migration-design.md`,
план `docs/.../plans/2026-07-20-...md`. Ветка `feat/operator-management-redesign`, коммиты
`4376e8cb`..`fa6d2905` (5 шт).

Сделано (только разметка/классы, логика/API/валидация 1:1):
- **Клуб**: `SettingsProfileSection` → `mgmt-form`; Валюта/Филиал = `.mgmt-meta-value` read-only
  строки (НЕ disabled-инпуты). Ключ `op.club.profileSection`.
- **Лояльность** (`LoyaltyTab`): три правила → карточки `RuleCard` (на MODULE scope — внутри тела
  компонента давало ремаунт percent-инпута/потерю фокуса, это анти-паттерн); живой пример
  начисления клиент-сайд (база 10000 minor, `Money signed`); лимиты + hint.
- **Шлюзы** (`PaymentGatewaysWorkspace`+`GatewaysTab`+`EskhataGatewayForm`): dcgate на kit,
  статус-чипы `ui-chip--status--xs` is-live/is-warning/is-neutral, `window.confirm`→
  `CriticalActionConfirmation`, `<hr>` убран, Telegram-стейт-машина 1:1. Легаси `.payment-card-*`
  CSS жил в `18-review.css` (не 15-settings).
- **Новости** (`NewsWorkspace`): форма+`<ul>` → `MgmtTable`+`MgmtDrawer` (create И edit
  all-in-drawer, sentinel `selectedId==='__new__'`, НЕ PanelModal — по выбору пользователя),
  удаление за confirm, `contentWidth=full`.

Статус: полный прогон 886/0/26skip, build зелёный, whole-branch review = ready to merge.
**НЕ смержено** — стоящий gate ветки = визуальная приёмка на глаз (см. [[operator-redesign-phase0-decisions]]).
Durable-уроки этой сессии закреплены — i18n источник = `/locales/{ru,en,tg}.json` + `cd packages/i18n && bun run gen`
(messages.ts генерируется), см. [[frontends-on-bun-test]]; component-in-component = ремаунт/потеря
фокуса, вспомогательные компоненты только на module scope.
