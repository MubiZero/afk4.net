---
name: shared-color-tokens
description: "Operator + Wizard palettes унифицированы в общий пакет @afk4/tokens (CSS-only, single source)"
metadata: 
  node_type: memory
  type: project
  originSessionId: 7ccf17c2-c4ca-4e42-bc98-f122d3732198
---

Палитра Operator.App.Web и SetupWizard.Web сведена в общий workspace-пакет **`@afk4/tokens`**
(`packages/tokens/tokens.css`) — единый источник цвета и шкал. Сделано на ветке
`feature/shared-color-tokens` (4 коммита: `31654ba` пакет, `ffe2c38` guard-тесты, `9b12568`
Operator, `9791b2e` Wizard); **ещё не смержено в main** на момент записи 2026-06-14.

Устройство пакета (CSS-only, JS-потребителей цвета в коде нет):
- `:root` — тема-независимые шкалы: `--radius-*`, `--duration-*`, `--ease-*`, `--focus-ring(-danger)`,
  типошкала `--text-xs..2xl`.
- `[data-theme="dark"]` / `[data-theme="light"]` — полный цвет (surfaces, borders, text-тиры,
  accent emerald, статусы, shadows). Акцент: dark `#2cc592` / light `#0b9e74`; `--accent-rgb`
  централизует бывшие хардкод-синие плитки.
- Палитра — «новый сводный набор»: нейтральный тёмный `#121212`-ramp (Material), slate-светлый,
  выверено по WCAG AA.

Подключение в каждом фронте: `import '@afk4/tokens/tokens.css'` в `main.tsx` ПЕРЕД `./styles.css`;
локальные блоки токенов из `styles.css` вырезаны; `dep "@afk4/tokens": "workspace:*"`. Оба
дефолтят в **тёмную** тему: `<html data-theme="dark">` + no-flash inline-скрипт в `index.html`
(ключи `afk4.operator.theme` / `afk4.setupWizard.theme`), убирающий вспышку не-той-темы.
Operator сохранил app-локальные алиасы `--panel`/`--line` локально (их нет в пакете).

Guard-тесты в `packages/tokens/tokens.test.ts` (bun test): (1) used-vars-defined — каждый
`var(--x)` фронта определён в пакете или локально (исключая `--chart-*`); (2) WCAG-контраст
с alpha-композитингом. Прогон через `~/.bun/bin/bun`.

**Вне scope (сознательно):** Player.Shell.Web (тот же сырой-CSS паттерн — следующий кандидат,
подключить так же); Platform.Web (Tailwind/shadcn, свой формат); Customer.Web (Tailwind +
tenant-брендинг через `branding/applyTheme.ts` — accent намеренно клиентский, не брендовый).

Спека: `docs/superpowers/specs/2026-06-14-shared-color-tokens-design.md`;
план: `docs/superpowers/plans/2026-06-14-shared-color-tokens.md`.

Связано: [[operator-theme-and-preview]], [[wizard-signin-redesign]], [[afk4-brand-positioning]].
