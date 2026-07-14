# Общий пакет токенов `@afk4/tokens`

**Дата:** 2026-06-14
**Статус:** дизайн на ревью
**Охват:** Operator.App.Web + SetupWizard.Web (Player.Shell.Web — отдельной работой позже)

## Проблема

Акцентная палитра двух «продуктовых» фронтов разъехалась: Operator сидел на консольном
синем (`#1f6feb`), Wizard — на брендовом emerald. Синий в Operator уже заменён на emerald
(предыдущая правка), но обе палитры по-прежнему живут как два независимых копипаст-набора
CSS-переменных в `styles.css` каждого приложения. Любая будущая правит снова разъедется.

Цель — единый источник истины для палитры, который оба приложения импортируют.

## Решение (кратко)

Новый workspace-пакет `packages/tokens` (рядом с `@afk4/i18n`, `@afk4/money`,
`@afk4/formatting`) отдаёт один файл `tokens.css` — единственный источник палитры и шкал.
CSS-only: JS-потребителей цвета в коде нет (проверено — ни `getComputedStyle`/`getPropertyValue`,
ни hex/rgb-литералов в `.ts/.tsx`), поэтому ни TS-выхода, ни кодогена не нужно (YAGNI).

Палитра — **новый сводный набор**: берётся выверенная основа Wizard (нейтральный тёмный
`#121212`-ramp по Material, тиры текста 87/60/38 %, чистый светлый slate-ramp) и расширяется
токенами, которых ему не хватает, но которые использует Operator (`--surface-card`,
`--text-strong`, `--border-accent`, `--accent-rgb`, `--accent-text`, `--accent-on-soft`,
`--accent-bright`). Акцент — emerald (залочен ранее). Контрасты текст-на-поверхности
проверяются guard-тестом по WCAG AA.

## Структура `tokens.css`

```
:root            { /* тема-независимые шкалы: radius, duration, ease, text-scale, focus-ring */ }
[data-theme="light"] { /* полный светлый набор цветов + тени */ }
[data-theme="dark"]  { /* полный тёмный набор цветов + тени */ }
```

Работает, потому что **оба приложения всегда явно ставят `data-theme` на `<html>`**
(Operator — `operatorTheme.tsx` useEffect; Wizard — `App.tsx:104`). Поэтому цветовые токены
живут только под `[data-theme]`, а не в bare `:root`.

## Полный набор токенов (объединение по обоим приложениям)

Цветовые (в каждой теме):

- **Accent:** `--accent`, `--accent-hover`, `--accent-pressed`, `--accent-soft`,
  `--accent-ring`, `--accent-rgb`, `--accent-text`, `--accent-on-soft`, `--accent-bright`,
  `--accent-glow`
- **Surfaces:** `--surface-canvas`, `--surface-elevated`, `--surface-card`, `--surface-muted`,
  `--surface-sunken`, `--surface-hover`, `--surface-accent-soft`
- **Borders:** `--border-soft`, `--border-default`, `--border-strong`, `--border-accent`
- **Text:** `--text-primary`, `--text-secondary`, `--text-tertiary`, `--text-quaternary`,
  `--text-strong`, `--text-on-accent`
- **Status:** `--danger`, `--danger-strong`, `--danger-text`, `--danger-soft-bg`,
  `--danger-soft-border`, `--warning`, `--warning-text`, `--warning-soft-bg`,
  `--warning-soft-border`, `--success`, `--success-text`, `--success-soft-bg`,
  `--success-soft-border`
- **Shadows** (тема-зависимы по alpha): `--shadow-card`, `--shadow-elevated`, `--shadow-press`

Тема-независимые шкалы (в `:root`):

- **Radius:** `--radius-xs/sm/md/lg/pill`
- **Duration:** `--duration-fast/medium/modal`
- **Easing:** `--ease-out/in/spring`
- **Focus:** `--focus-ring` (= `0 0 0 3px var(--accent-ring)`), `--focus-ring-danger`
- **Type scale:** `--text-xs/sm/base/md/lg/xl/2xl`

App-локальные алиасы НЕ переезжают в пакет, остаются в `styles.css` приложения:
Operator `--panel` (= `var(--surface-elevated)`), `--line` (= `var(--border-default)`),
`--chart-color`, `@property --chart-value`.

## Канонические значения

### Тёмная тема
| токен | значение |
|---|---|
| surface-canvas | `#121212` |
| surface-elevated | `#1e1e1e` |
| surface-card | `#242424` |
| surface-muted | `#1a1a1a` |
| surface-sunken | `#161616` |
| surface-hover | `#232323` |
| surface-accent-soft | `#143830` |
| border-soft | `#2a2a2a` |
| border-default | `#3a3a3a` |
| border-strong | `#555555` |
| border-accent | `#2f6b58` |
| text-primary | `rgba(255,255,255,0.92)` |
| text-strong | `rgba(255,255,255,0.82)` |
| text-secondary | `rgba(255,255,255,0.70)` |
| text-tertiary | `rgba(255,255,255,0.55)` |
| text-quaternary | `rgba(255,255,255,0.38)` |
| text-on-accent | `#0a1a14` |
| accent | `#2cc592` |
| accent-hover | `#3dd9a3` |
| accent-pressed | `#1ea877` |
| accent-soft | `#143830` |
| accent-ring | `rgba(44,197,146,0.32)` |
| accent-rgb | `44, 197, 146` |
| accent-glow | `rgba(44,197,146,0.30)` |
| accent-text | `#4dd6a6` |
| accent-on-soft | `#c2f0df` |
| accent-bright | `#effbf6` |
| danger / -strong / -text | `#f87171` / `#c4261d` / `#fca5a5` |
| danger-soft-bg / -border | `#2a1414` / `#5a2424` |
| warning / -text | `#fbbf24` / `#fcd34d` |
| warning-soft-bg / -border | `#2a1f0a` / `#5a3f18` |
| success / -text | `#4ade80` / `#86efac` |
| success-soft-bg / -border | `#15311f` / `#1f5135` |
| shadow-card | `0 1px 0 rgba(0,0,0,.30), 0 8px 24px rgba(0,0,0,.40)` |
| shadow-elevated | `0 1px 0 rgba(0,0,0,.35), 0 18px 40px rgba(0,0,0,.50)` |
| shadow-press | `0 6px 14px var(--accent-glow)` |

### Светлая тема
| токен | значение |
|---|---|
| surface-canvas | `#eef2f7` |
| surface-elevated | `#ffffff` |
| surface-card | `#f7f9fc` |
| surface-muted | `#f8fafc` |
| surface-sunken | `#f1f5f9` |
| surface-hover | `#f1f7f4` |
| surface-accent-soft | `#dcefe6` |
| border-soft | `#e2e8f0` |
| border-default | `#cbd5e1` |
| border-strong | `#94a3b8` |
| border-accent | `#8ed0b6` |
| text-primary | `#0f172a` |
| text-strong | `#334155` |
| text-secondary | `#475569` |
| text-tertiary | `#64748b` |
| text-quaternary | `#94a3b8` |
| text-on-accent | `#ffffff` |
| accent | `#0b9e74` |
| accent-hover | `#0a8a66` |
| accent-pressed | `#07664c` |
| accent-soft | `#d9e6e1` |
| accent-ring | `rgba(11,158,116,0.22)` |
| accent-rgb | `11, 158, 116` |
| accent-glow | `rgba(11,158,116,0.24)` |
| accent-text | `#0b9e74` |
| accent-on-soft | `#07664c` |
| accent-bright | `#06402f` |
| danger / -strong / -text | `#dc2626` / `#c4261d` / `#991b1b` |
| danger-soft-bg / -border | `#fff1f2` / `#fecaca` |
| warning / -text | `#d97706` / `#92400e` |
| warning-soft-bg / -border | `#fffbeb` / `#fde68a` |
| success / -text | `#16a34a` / `#15803d` |
| success-soft-bg / -border | `#ecfdf3` / `#bbf7d0` |
| shadow-card | `0 1px 0 rgba(15,23,42,.04), 0 8px 24px rgba(15,23,42,.08)` |
| shadow-elevated | `0 1px 0 rgba(15,23,42,.04), 0 18px 40px rgba(15,23,42,.12)` |
| shadow-press | `0 6px 14px var(--accent-glow)` |

### Шкалы (`:root`, обе темы)
radius: xs 4 / sm 6 / md 8 / lg 12 / pill 999px ·
duration: fast 120ms / medium 200ms / modal 320ms ·
ease-out `cubic-bezier(0.16,1,0.3,1)` / ease-in `cubic-bezier(0.4,0,1,1)` /
ease-spring `cubic-bezier(0.34,1.4,0.7,1)` ·
focus-ring `0 0 0 3px var(--accent-ring)` / focus-ring-danger `0 0 0 3px rgba(220,38,38,0.22)` ·
text: xs 11 / sm 13 / base 14 / md 16 / lg 20 / xl 26 / 2xl 32 (px)

## Подключение в приложениях

1. В entry (`main.tsx`) импорт пакета **до** локальных стилей:
   `import "@afk4/tokens/tokens.css"; import "./styles.css";`
   (JS-импорт надёжнее CSS `@import` в Vite и снимает вопрос порядка правил.)
2. Из `styles.css` каждого приложения удаляются локальные блоки токенов (`:root` цвета,
   `[data-theme=...]`, локальные radius/duration/ease/text/focus). Остаются только app-правила
   и app-локальные алиасы.
3. Анти-флэш: оба фронта дефолтят в **тёмную** тему (киберклуб/ночь — правило контекста).
   - **Wizard** уже имеет inline-скрипт в `index.html` (дефолт `dark`, ключ
     `afk4.setupWizard.theme`) — оставляем как есть.
   - **Operator** сейчас НЕ ставит `data-theme` до монтирования (только useEffect) → после
     удаления цветов из bare `:root` будет кадр «нет цвета». Добавляем такой же inline-скрипт
     (дефолт `dark`, ключ `afk4.operator.theme`), зеркалящий `operatorTheme.tsx`.
4. Зависимость `"@afk4/tokens": "workspace:*"` в `package.json` обоих фронтов.
   Корневые `workspaces` уже включают `packages/*` — пакет подхватится автоматически.

## Пакет

```
packages/tokens/
  package.json        # name @afk4/tokens, exports "./tokens.css"
  tokens.css          # единственный источник
  tokens.test.ts      # guard-тесты (bun test; pure node:fs, без happy-dom — bunfig не нужен)
  tsconfig.json
```

## Тесты (guard)

1. **used-vars-defined** — парсит `styles.css` Operator и Wizard, собирает все `var(--x)` и
   проверяет, что каждый определён либо в `tokens.css`, либо локально в том же `styles.css`
   (так app-локальные `--panel`/`--line`/`--chart-*` проходят без хардкод-списка исключений).
   Ловит будущий дрейф «использую переменную, которой нет».
2. **wcag-contrast** — считает контраст ключевых пар (text-primary/secondary/tertiary ×
   surface-canvas/elevated/card) в обеих темах, требует AA (основной текст ≥ 4.5:1,
   tertiary как вторичный ≥ 3:1).

## Проверка готовности

`tsc -b` + `bun test` + `vite build` для обоих фронтов зелёные; визуальный смоук в mock-режиме
Operator (`bun run dev`) и preview Wizard — обе темы, переключатель, отсутствие флэша.

## Вне scope

Player.Shell.Web (подключение позже, тот же паттерн); Platform.Web и Customer.Web
(другой стек — Tailwind/shadcn; у Customer accent намеренно tenant-брендируемый).
```
