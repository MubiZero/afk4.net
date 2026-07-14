# Operator UI-kit — кросс-секционная уборка: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use `- [ ]`.
> **Субагентам:** работать на текущем checkout, БЕЗ git worktree/checkout/stash (в S3 worktree сбил HEAD).

**Goal:** Убрать архитектурный смелл — вынести общие примитивы (`StateFlag`, `CriticalActionConfirmation`)
из секционного `06-map-grid.css` в общий слой; `StateFlag → .ui-chip--count` (+ фикс money-span бага).

**Architecture:** `.ui-chip--count` (зарезервирован в `02-ui-kit.css`) достраивается до паритета со
`.state-flag`, `StateFlag`-компонент переводится на него, `.state-flag` CSS удаляется из map-файла.
`.critical-confirmation-actions` CSS дословно переносится в `16-workspace-primitives.css`. Вид во всех
6+ разделах-потребителях НЕ меняется (кроме бонус-фикса: деньги в шапке Кассы перестают сжиматься).

**Tech Stack:** React + plain CSS, Vite (`tsc -b && vite`), `bun test` (happy-dom + jest-dom), i18n.

## Global Constraints

- **Визуальный паритет во ВСЕХ потребителях** — главный критерий. StateFlag: Клиенты/Логи/Касса/Review/
  Склад/Брони. Confirmation: Карта/Касса/Склад/Настройки×2/PaymentDialog.
- **Никаких изменений денежной логики** (`*Model.ts`). i18n — существующие ключи.
- **Каскад:** `06-map-grid.css` (import #6) грузится ДО `16-workspace-primitives.css` (#16) и `02-ui-kit.css`
  (#2). Перенос `.critical-confirmation-actions` в 16 = загрузка позже → каскад сохранён/усилен.
- **Гейт:** `bun test` + `App.test` + `bun run build` зелёные.
- Субагентам — БЕЗ git worktree.

---

### Task 1: `StateFlag → .ui-chip--count` (достройка атома + миграция примитива)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css` (достроить `.ui-chip--count`)
- Modify: `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx` (`StateFlag` рендер)
- Modify: `src/AFK4.Operator.App.Web/src/styles/06-map-grid.css` (удалить `.state-flag*`)
- Test: любые `*.test.tsx`, ассертящие `.state-flag` (найти грепом)

- [ ] **Step 1: Достроить `.ui-chip--count` в `02-ui-kit.css` до паритета со `.state-flag`**

Прочитать текущие `.state-flag*` правила в `06-map-grid.css`: блок `447–` (`.state-flag`,
`.state-flag strong`, `.state-flag.critical`, `.state-flag.critical::before`, `.state-flag.critical span`,
и `.state-flag.warning`/`.warning::before`/`.warning span` ниже) + метку в комбинированном правиле
(`.screen-head span, … .state-flag span { color: var(--text-tertiary); font-size: 10px; }`).

В `02-ui-kit.css` расширить существующий `.ui-chip--count` блок (строки ~141–153), чтобы он ВОСПРОИЗВОДИЛ
`.state-flag` байт-в-байт, но с адаптациями:
- контейнер `.ui-chip--count`: добавить `padding: 0 9px;` (у `.ui-chip` база = `0 var(--space-3)`=12px,
  а `.state-flag` = 9px). Остальное (gap 7px, height 26px, bg surface-elevated, border 1px default, pill)
  уже даёт `.ui-chip`+существующий `--count`.
- **метка — ПРЯМОЙ ребёнок** (критично для money-span фикса): `.ui-chip--count > span { color: var(--text-tertiary); font-size: 10px; }`.
- значение: `.ui-chip--count > strong { color: var(--text-primary); font-size: 11px; }` (у `.state-flag strong`
  =11px; текущий `--count strong` = `--text-xs`; если `--text-xs`≠11px — задать 11px явно для паритета;
  скоуп `> strong`).
- `.ui-chip--count.is-critical { border-color: var(--border-strong); }` +
  `.ui-chip--count.is-critical::before { width:6px; height:6px; border-radius: var(--radius-pill); background: var(--danger); content:""; }` +
  `.ui-chip--count.is-critical > span { color: var(--text-secondary); }` (метка ярче при critical — из `.state-flag.critical span`).
- `.ui-chip--count.is-warning` — если `.state-flag.warning` задаёт border-strong / bg / span-цвет, воспроизвести
  так же (`.is-warning::before` жёлтая точка уже есть — сверить значения).

Убрать `.ui-chip--count` из reserved-комментария, если он там перечислен.

- [ ] **Step 2: Перевести `StateFlag`-компонент на `.ui-chip--count`**

В `operatorPrimitives.tsx` (`StateFlag`, ~строка 61):
```tsx
export function StateFlag({ label, value, critical, tone }: { label: string; value: ReactNode; critical?: boolean; tone?: 'warning' }) {
  return (
    <section className={`ui-chip ui-chip--count${critical ? ' is-critical' : ''}${tone ? ` is-${tone}` : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </section>
  );
}
```
(внутренняя разметка `<span>label</span><strong>value</strong>` — та же; меняются только классы обёртки).

- [ ] **Step 3: Обновить тесты, ассертящие `.state-flag`**

`grep -rn "state-flag" src` — если тесты ищут `.state-flag`/`.critical`, перевести на `.ui-chip--count`/
`.is-critical`/`.is-warning` (не ослабляя проверку).

- [ ] **Step 4: Удалить `.state-flag*` из `06-map-grid.css`**

Удалить весь блок `.state-flag*` (`447–` включая `.critical`/`.warning`/`::before`/` span` варианты) и
УБРАТЬ `.state-flag span` из комбинированного label-правила (`.screen-head span, …, .state-flag span {…}`)
— оставив прочие селекторы (`.screen-head span`, `.detail-row span` и т.д.). `grep -rn "\bstate-flag\b" src`
после — 0 ссылок в TSX/CSS (кроме, возможно, комментариев).

- [ ] **Step 5: Гейт**

`cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun test src/App.test.tsx && /home/fedya/.bun/bin/bun run build` — всё зелёное.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css \
        src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx \
        src/AFK4.Operator.App.Web/src/styles/06-map-grid.css
git commit -m "refactor(operator): StateFlag → .ui-chip--count (общий атом), .state-flag CSS из map-файла удалён + money-span фикс метки"
```

---

### Task 2: Перенос `.critical-confirmation-actions` CSS в общий слой

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/06-map-grid.css` (вырезать `.critical-confirmation-actions*`)
- Modify: `src/AFK4.Operator.App.Web/src/styles/16-workspace-primitives.css` (вставить те же правила)

- [ ] **Step 1: Найти границы блока**

В `06-map-grid.css` найти ВСЕ правила `.critical-confirmation-actions*` (контейнер + ` button` +
`button.danger` + `button.accent` + hover/focus/disabled варианты; разведка указывала диапазон ~828–1220 —
проверить точные границы грепом `grep -n "critical-confirmation" src/styles/06-map-grid.css`).

- [ ] **Step 2: Перенести ДОСЛОВНО в `16-workspace-primitives.css`**

Вырезать все `.critical-confirmation-actions*` правила из `06-map-grid.css` и вставить БАЙТ-В-БАЙТ в конец
`16-workspace-primitives.css` (где живут стили примитивов). Правила не менять — pure move. `16` грузится
после `06` → каскад сохранён/усилен.

- [ ] **Step 3: Проверить, что ничего между 06 и 16 не полагалось на прежнюю позицию**

`grep -rn "critical-confirmation" src/styles/*.css` — правила теперь только в `16`; в `06` их нет.
Убедиться, что ни один селектор в `07–15` не переопределял `.critical-confirmation-actions` (грепом);
если переопределял — учесть (маловероятно, блок map-специфичный).

- [ ] **Step 4: Гейт**

`cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun test src/App.test.tsx && /home/fedya/.bun/bin/bun run build` — зелёное.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/06-map-grid.css \
        src/AFK4.Operator.App.Web/src/styles/16-workspace-primitives.css
git commit -m "refactor(operator): .critical-confirmation-actions CSS из map-файла в 16-workspace-primitives (общий примитив — общий слой)"
```

---

## Self-Review

- §2.1 StateFlag→count: Task 1 (атом достройка + компонент + удаление .state-flag + money-span фикс метки `> span`). ✓
- §2.2 relocation: Task 2 (дословный перенос в 16). ✓
- §4 гейт/паритет: обе задачи + финальный whole-branch review + live-превью (StateFlag в 6 разделах,
  confirmation в 6 разделах). ✓
- Placeholder scan: значения `.state-flag` даны на чтение (точные в CSS); шаги конкретны.
- Каскад: 16 после 06 — проверка в Task 2 Step 3.
