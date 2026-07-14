---
name: operator-surface-elevation
description: "Operator surface-иерархия — на светлой теме глубина = ПОДЪЁМ (белая панель + мягкая тень), НЕ затемнение/recessed."
metadata:
  node_type: memory
  type: project
  originSessionId: 9c6b297d-3697-4eed-a2e2-12aef2dd37ac
---

Инвариант иерархии поверхностей оператора (мировая практика: Material 3 / Apple grouped backgrounds / Stripe·Linear).

**Светлая тема:** глубину даёт **элевация**, а не «утопление». Серый холст-страница (`--surface-canvas`) → **белые приподнятые панели** (`--surface-elevated`) с hairline-ободком (`--border-soft`) + мягкой слоистой тенью (`--shadow-card` — 2 слоя, низкая прозрачность). «Recessed/темнее фона» — идиома **тёмной** темы (там осветлять нельзя, поэтому углубляют); на светлой применять = ошибка (панель сливается с фоном).
- Грабли, в которые вляпались: гонял `--surface-sunken` темнее холста по глазомеру — это борьба с конвенцией. Правильный рычаг = тень+белая панель, а не оттенок серого. `--surface-sunken` на light НЕ должен быть темнее `--surface-canvas` бессмысленно — он для инпутов/треков (лёгкий inset на белой панели).

**Паттерн floating-panel (канон для всех разделов):** каждая верхнеуровневая секционная панель/бар на холсте = `background: --surface-elevated; border: 1px solid --border-soft; box-shadow: var(--shadow-card)`. Раскатано: POS (`.pos-panel`, лента заказов), map (`.map-header`,`.context-panel`), dashboard (группа `.dashboard-*-panel`), booking (`.booking-header`,`.booking-requests-lane`), clients (`.clients-head`,`.clients-panel`), payments (`.payments-panel`), logs (`.logs-panel`), settings (`.settings-main-panel`,`.settings-card-panel`), stock (`.cash-stock-levels`,`.stock-receiving`,`.stock-journal`).
- НЕ давать тень: модалкам/поповерам/дропдаунам (у них `--shadow-elevated`), инпутам/чипам/строкам/кнопкам, и **вложенным** боксам внутри панели (иначе card-in-card — анти-паттерн из [[interface-limb]]-линз).
- Плитки/ячейки внутри панели — НЕ мини-карточки (card-in-card), а bordered-ячейки, всплывают тенью+акцентным ободком на hover (`.pos-product-card`).

Токены/тесты: тени уже есть в `packages/tokens/tokens.css` (`--shadow-card`/`--shadow-elevated`, на dark почти не видны — там элевация через светлоту). WCAG-гард в `packages/tokens/tokens.test.ts` проверяет текст на canvas/elevated/card (НЕ на sunken). Связано: [[shared-color-tokens]], [[operator-theme-and-preview]].
