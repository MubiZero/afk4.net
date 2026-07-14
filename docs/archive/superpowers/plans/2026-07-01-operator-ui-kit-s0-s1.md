# Operator UI-kit — S0 + S1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ввести общий CSS-слой атомов (`.ui-*`) + два тонких React-компонента (`Money`, `LedgerRow`) без визуальных изменений (S0), затем перевести экран «Клиенты» на этот слой и применить UX-фиксы §7 спеки (S1).

**Architecture:** Новый `styles/02-ui-kit.css` содержит атомы `.ui-btn / .ui-chip / .ui-card / .ui-field / .ui-money / .ui-ledger-*`, извлечённые из «эталонных» разделов (Касса/Карта/Брони) — только токены. `Money` централизует формат денег (mono + tabular-nums), `LedgerRow` — строку операции (используется в истории и мини-ленте кошелька). Компоненты «Клиентов» переключают `className` на атомы; секционные дубли-правила удаляются из `12-players.css`.

**Tech Stack:** React 18 + TypeScript, обычный CSS (barrel `styles.css` через `@import`), сборка Vite (`tsc -b && vite build`), тесты `bun test` (happy-dom + @testing-library/react + jest-dom), i18n `@afk4/i18n`, деньги `@afk4/money` + `currencyFormat.ts`.

## Global Constraints

- **Только токены.** В новом/мигрированном коде запрещены сырые значения: отступы → `--space-*`; скругления → `--radius-*`; высоты контролов → `--control-sm|md|lg`; типографика → `--text-xs|sm|base|lg|xl|2xl`; motion → `--duration-fast|medium|modal` + `--ease-out|in`; фокус → `--focus-ring`. Источник: `packages/tokens/tokens.css`.
- **Деньги/время — mono.** Любая сумма/время/количество рендерится `font-family: var(--font-mono); font-variant-numeric: tabular-nums;` (через `Money`/`.ui-money`/`.ui-ledger-time`).
- **Money-path FROZEN.** Не менять логику расчёта сумм, валидацию сумм на сервере, DTO. Меняем только представление. Значения приходят в minor units; формат — через `formatMinorUnits(minorUnits, currencyCode)`.
- **i18n.** Новых строк по возможности не вводить — переиспользовать существующие ключи `op.players.*`. Если ключ действительно новый — добавить во все локали (ru/en/tg), tg = настоящий таджикский, не копия ru.
- **Гейт слайса.** Каждый слайс завершается зелёными `bun test` **и** `bun run build` (последнее тайпчекает и тесты, и сужения типов). Команды запускать из `src/AFK4.Operator.App.Web`.
- **Namespace `.ui-`.** Все атомы под этим префиксом.
- **Каскад.** `02-ui-kit.css` грузится ДО секционных файлов; порядок остальных `@import` не менять.
- **Без AI-подписей** в коммитах/коде.
- **Ветка:** `feat/operator-ui-kit` (уже создана от `main`, содержит спеку `d69d0839`).

**Пути (все от корня репозитория):**
- Web-корень: `src/AFK4.Operator.App.Web/`
- Стили: `src/AFK4.Operator.App.Web/src/styles/`
- Компоненты Клиентов: `src/AFK4.Operator.App.Web/src/players/`
- Примитивы: `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx`
- Формат денег: `src/AFK4.Operator.App.Web/src/currencyFormat.ts` (реэкспорт из `operatorHelpers.ts:880`)

---

## S0 — Общий слой (без визуальных изменений)

### Task S0.1: Файл `02-ui-kit.css` с атомами + подключение в barrel

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css` (вставить `@import` после строки 4 `01-base.css`, до `02-shell.css`)

**Interfaces:**
- Produces (CSS-классы для последующих задач): `.ui-btn` (+ `--primary|--danger|--ghost|--sm|--lg|--block`), `.ui-chip` (+ `--filter.is-active`, `--status.is-live|is-booking|is-danger|is-warning|is-neutral`, `--xs`, `--count.is-warning`), `.ui-card` (+ `--interactive|--edge|--stat.is-danger`), `.ui-field` (+ `.ui-field-error`), `.ui-money` (+ `--pos|--neg`), `.ui-ledger-list`, `.ui-ledger-row` (+ `--compact`), `.ui-ledger-time|body|title|detail|aside|refund|reversal`.

- [ ] **Step 1: Создать `styles/02-ui-kit.css` с полным содержимым**

```css
/* @afk4 UI-kit — общие атомы визуального языка оператора (btn/chip/card/field/money/ledger).
   Извлечены из «эталонных» разделов (Касса/Карта/Брони): только токены, деньги = mono+tabular,
   карточка = кант + подъём тенью, состояние = цвет. Грузится сразу после 01-base, до секционных
   стилей: во время миграции секционные правила ещё выигрывают по каскаду; после переезда раздела
   его дубли-атомы удаляются. Namespace .ui-*. */

/* ============ Button ============ */
.ui-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-1);
  height: var(--control-md);
  padding: 0 var(--space-3);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
  color: var(--text-strong);
  font-size: var(--text-xs);
  font-weight: 600;
  white-space: nowrap;
  cursor: pointer;
  transition: background var(--duration-fast) var(--ease-out),
    border-color var(--duration-fast) var(--ease-out),
    box-shadow var(--duration-fast) var(--ease-out),
    transform var(--duration-fast) var(--ease-out);
}
.ui-btn:hover:not(:disabled) { border-color: var(--border-accent); }
.ui-btn:active:not(:disabled) { transform: scale(0.98); }
.ui-btn:focus-visible { outline: none; box-shadow: var(--focus-ring); }
.ui-btn:disabled {
  border-color: var(--border-default);
  background: var(--surface-muted);
  color: var(--text-quaternary);
  cursor: not-allowed;
}
.ui-btn--sm { height: var(--control-sm); }
.ui-btn--lg { height: var(--control-lg); font-size: var(--text-sm); }
.ui-btn--block { width: 100%; }

.ui-btn--primary {
  border-color: var(--accent);
  background: var(--accent);
  color: var(--text-on-accent);
  font-weight: 700;
}
.ui-btn--primary:hover:not(:disabled) {
  border-color: var(--accent-hover);
  background: var(--accent-hover);
  box-shadow: var(--shadow-press);
}
.ui-btn--primary:disabled {
  border-color: var(--border-default);
  background: var(--surface-muted);
  color: var(--text-quaternary);
}

.ui-btn--danger {
  border-color: var(--danger-soft-border);
  background: transparent;
  color: var(--danger-text);
}
.ui-btn--danger:hover:not(:disabled) {
  border-color: var(--danger);
  background: var(--danger-soft-bg);
}

.ui-btn--ghost {
  border-color: transparent;
  background: none;
  color: var(--accent-text);
  padding: 0 var(--space-2);
}
.ui-btn--ghost:hover:not(:disabled) { background: var(--surface-hover); }

/* ============ Chip ============ */
.ui-chip {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  height: var(--control-sm);
  padding: 0 var(--space-3);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-pill);
  background: var(--surface-card);
  color: var(--text-secondary);
  font-size: var(--text-xs);
  font-weight: 600;
  white-space: nowrap;
  transition: border-color var(--duration-fast) var(--ease-out),
    background var(--duration-fast) var(--ease-out),
    color var(--duration-fast) var(--ease-out);
}
.ui-chip > b { color: var(--text-primary); font-weight: 700; }

.ui-chip--filter { cursor: pointer; }
.ui-chip--filter:hover { border-color: var(--border-accent); }
.ui-chip--filter:focus-visible { outline: none; box-shadow: var(--focus-ring); }
.ui-chip--filter.is-active {
  border-color: var(--accent);
  background: var(--surface-accent-soft);
  color: var(--accent-bright);
}

.ui-chip--status { gap: 6px; }
.ui-chip--status.is-live {
  border-color: var(--success-soft-border);
  background: var(--success-soft-bg);
  color: var(--success-text);
}
.ui-chip--status.is-booking {
  border-color: var(--border-accent);
  background: var(--surface-accent-soft);
  color: var(--accent-bright);
}
.ui-chip--status.is-danger {
  border-color: var(--danger-soft-border);
  background: var(--danger-soft-bg);
  color: var(--danger-text);
}
.ui-chip--status.is-warning {
  border-color: var(--warning-soft-border);
  background: var(--warning-soft-bg);
  color: var(--warning-text);
}
.ui-chip--status.is-neutral {
  border-color: var(--border-default);
  background: var(--surface-muted);
  color: var(--text-secondary);
}

/* маленький инлайн-бейдж рядом с текстом (напр. «Долг»/«Неактивен»/долг с суммой) */
.ui-chip--xs {
  height: auto;
  padding: 1px var(--space-2);
  font-weight: 700;
}

/* счётчик с меткой+значением (шапка) */
.ui-chip--count {
  gap: 7px;
  height: 26px;
  background: var(--surface-elevated);
}
.ui-chip--count strong { color: var(--text-primary); font-size: var(--text-xs); }
.ui-chip--count.is-warning::before {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--warning);
  content: "";
}

/* ============ Card ============ */
.ui-card {
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  background: var(--surface-card);
}
.ui-card--interactive {
  cursor: pointer;
  transition: border-color var(--duration-fast) var(--ease-out),
    background var(--duration-fast) var(--ease-out),
    box-shadow var(--duration-fast) var(--ease-out),
    transform var(--duration-fast) var(--ease-out);
}
.ui-card--interactive:hover,
.ui-card--interactive:focus-visible {
  border-color: var(--accent);
  background: var(--surface-hover);
  box-shadow: var(--shadow-card);
  outline: none;
  transform: translateY(-1px);
}
.ui-card--edge { border-left: 3px solid var(--edge, transparent); }

.ui-card--stat {
  display: grid;
  gap: var(--space-1);
  background: var(--surface-elevated);
}
.ui-card--stat > span { color: var(--text-secondary); font-size: var(--text-xs); }
.ui-card--stat > strong { color: var(--text-primary); font-size: var(--text-lg); line-height: 1; }
.ui-card--stat.is-danger { border-color: var(--danger-soft-border); background: var(--danger-soft-bg); }
.ui-card--stat.is-danger > strong { color: var(--danger-text); }

/* ============ Field ============ */
.ui-field { display: grid; gap: var(--space-1); }
.ui-field > label { color: var(--text-secondary); font-size: var(--text-xs); font-weight: 600; }
.ui-field > input,
.ui-field > select {
  height: var(--control-md);
  min-width: 0;
  padding: 0 var(--space-3);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  background: var(--surface-sunken);
  color: var(--text-primary);
  font-size: var(--text-xs);
  outline: 0;
  transition: border-color var(--duration-fast) var(--ease-out),
    box-shadow var(--duration-fast) var(--ease-out);
}
.ui-field > input::placeholder { color: var(--text-quaternary); }
.ui-field > input:focus,
.ui-field > select:focus {
  border-color: var(--accent);
  box-shadow: var(--focus-ring);
}
.ui-field-error { color: var(--danger-text); font-size: var(--text-xs); }

/* ============ Money ============ */
.ui-money {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
.ui-money--pos { color: var(--success-text); }
.ui-money--neg { color: var(--danger-text); }

/* ============ Ledger row ============ */
.ui-ledger-list { display: flex; flex-direction: column; }
.ui-ledger-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-2) var(--space-3);
  border-bottom: 1px solid var(--border-soft);
}
.ui-ledger-row:last-child { border-bottom: 0; }
.ui-ledger-time {
  color: var(--text-tertiary);
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: var(--text-xs);
}
.ui-ledger-body { display: grid; gap: 2px; min-width: 0; }
.ui-ledger-title { color: var(--text-primary); font-size: var(--text-sm); font-weight: 600; }
.ui-ledger-detail {
  color: var(--text-tertiary);
  font-size: var(--text-xs);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ui-ledger-aside { display: inline-flex; align-items: center; gap: var(--space-2); justify-self: end; }
.ui-ledger-reversal {
  margin-left: var(--space-2);
  color: var(--text-tertiary);
  font-size: var(--text-xs);
  font-style: normal;
}
/* «Вернуть» проявляется на hover/фокусе строки; на сенсорных экранах — всегда видно */
.ui-ledger-refund { opacity: 0; transition: opacity var(--duration-fast) var(--ease-out); }
.ui-ledger-row:hover .ui-ledger-refund,
.ui-ledger-row:focus-within .ui-ledger-refund { opacity: 1; }
@media (hover: none) { .ui-ledger-refund { opacity: 1; } }
.ui-ledger-row--compact .ui-ledger-detail { display: none; }
```

- [ ] **Step 2: Подключить в barrel `styles.css`**

Открыть `src/AFK4.Operator.App.Web/src/styles.css`. После строки `@import './styles/01-base.css';` вставить новую строку:

```css
@import './styles/01-base.css';
@import './styles/02-ui-kit.css';
@import './styles/02-shell.css';
```

(Остальные `@import` не трогать.)

- [ ] **Step 3: Сборка — убедиться, что ничего не сломалось и вид не изменился**

Run (из `src/AFK4.Operator.App.Web`): `bun run build`
Expected: PASS (сборка зелёная). Классы `.ui-*` пока никем не используются → визуально экран не меняется.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-ui): общий слой атомов 02-ui-kit.css (.ui-btn/.ui-chip/.ui-card/.ui-field/.ui-money/.ui-ledger)"
```

---

### Task S0.2: Компонент `Money`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx` (добавить экспорт `Money`)
- Test: `src/AFK4.Operator.App.Web/src/operatorPrimitives.money.test.tsx` (создать)

**Interfaces:**
- Consumes: `formatMinorUnits(minorUnits: number, currencyCode: string): string` из `./currencyFormat`; класс `.ui-money`/`--pos`/`--neg` из S0.1.
- Produces: `Money({ minorUnits: number; currencyCode: string; signed?: boolean }): JSX.Element` — при `signed` префиксует `+`/`−`, красит тон (`--pos`/`--neg`) и форматирует `Math.abs`; иначе форматирует значение как есть. Всегда mono (класс `.ui-money`).

- [ ] **Step 1: Написать падающий тест**

Создать `src/AFK4.Operator.App.Web/src/operatorPrimitives.money.test.tsx`:

```tsx
import { describe, expect, it, afterEach } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { Money } from './operatorPrimitives';

afterEach(cleanup);

describe('Money', () => {
  it('renders unsigned amount with the localized currency sign', () => {
    render(<Money minorUnits={45000} currencyCode="TJS" />);
    expect(screen.getByText('450 с.')).toHaveClass('ui-money');
  });

  it('prefixes + and marks positive tone when signed', () => {
    render(<Money minorUnits={50000} currencyCode="TJS" signed />);
    expect(screen.getByText('+500 с.')).toHaveClass('ui-money--pos');
  });

  it('prefixes − and marks negative tone when signed', () => {
    render(<Money minorUnits={-12000} currencyCode="TJS" signed />);
    expect(screen.getByText('−120 с.')).toHaveClass('ui-money--neg');
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/operatorPrimitives.money.test.tsx`
Expected: FAIL (`Money` не экспортируется).

- [ ] **Step 3: Реализовать `Money`**

В `src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx` добавить импорт в начало файла (после существующих import):

```tsx
import { formatMinorUnits } from './currencyFormat';
```

И добавить экспорт компонента (в конец файла):

```tsx
export function Money({
  minorUnits,
  currencyCode,
  signed = false
}: {
  minorUnits: number;
  currencyCode: string;
  signed?: boolean;
}) {
  if (!signed) {
    return <span className="ui-money">{formatMinorUnits(minorUnits, currencyCode)}</span>;
  }
  const positive = minorUnits >= 0;
  const text = formatMinorUnits(Math.abs(minorUnits), currencyCode);
  return (
    <span className={`ui-money ${positive ? 'ui-money--pos' : 'ui-money--neg'}`}>
      {positive ? '+' : '−'}{text}
    </span>
  );
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `bun test src/operatorPrimitives.money.test.tsx`
Expected: PASS (3 теста).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx src/AFK4.Operator.App.Web/src/operatorPrimitives.money.test.tsx
git commit -m "feat(operator-ui): компонент Money — mono-формат денег со знаком и тоном"
```

---

### Task S0.3: Компонент `LedgerRow`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/LedgerRow.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/LedgerRow.test.tsx`

**Interfaces:**
- Consumes: `Money` (S0.2); `LedgerEntryView` из `./playersModel` (поля: `id, timeLabel, typeLabel, description, reason, amountMinorUnits, currencyCode, isCredit, isReversal`); классы `.ui-ledger-*` (S0.1); ключи i18n `op.players.history.reversalBadge`, `op.players.refund.rowBtn`.
- Produces: `LedgerRow({ view: LedgerEntryView; currencyCode: string; compact?: boolean; canRefund?: boolean; onRefund?: () => void }): JSX.Element`. В `compact` скрывает подпись и «Вернуть». «Вернуть» рендерится только при `canRefund && !view.isReversal && onRefund`.

- [ ] **Step 1: Написать падающий тест**

Создать `src/AFK4.Operator.App.Web/src/players/LedgerRow.test.tsx`:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { LedgerRow } from './LedgerRow';
import type { LedgerEntryView } from './playersModel';

afterEach(cleanup);

const view: LedgerEntryView = {
  id: 'le-1',
  timeLabel: '04:00',
  typeLabel: 'Пополнение',
  description: 'Пополнение кошелька',
  reason: 'Касса',
  amountMinorUnits: 50000,
  currencyCode: 'TJS',
  isCredit: true,
  isReversal: false
};

const renderRow = (over: Partial<Parameters<typeof LedgerRow>[0]> = {}) =>
  render(
    <I18nProvider initialLocale="ru">
      <LedgerRow view={view} currencyCode="TJS" {...over} />
    </I18nProvider>
  );

describe('LedgerRow', () => {
  it('renders time, type, detail and a signed positive amount', () => {
    renderRow();
    expect(screen.getByText('04:00')).toBeInTheDocument();
    expect(screen.getByText('Пополнение')).toBeInTheDocument();
    expect(screen.getByText('Пополнение кошелька · Касса')).toBeInTheDocument();
    expect(screen.getByText('+500 с.')).toHaveClass('ui-money--pos');
  });

  it('hides detail and refund in compact variant', () => {
    renderRow({ compact: true, canRefund: true, onRefund: () => {} });
    expect(screen.queryByText('Пополнение кошелька · Касса')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('fires onRefund from the row action when refundable', () => {
    const onRefund = mock(() => {});
    renderRow({ canRefund: true, onRefund });
    fireEvent.click(screen.getByRole('button', { name: /Вернуть/ }));
    expect(onRefund).toHaveBeenCalled();
  });

  it('never shows refund for a reversal entry', () => {
    renderRow({ view: { ...view, isReversal: true }, canRefund: true, onRefund: () => {} });
    expect(screen.queryByRole('button')).toBeNull();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/players/LedgerRow.test.tsx`
Expected: FAIL (`./LedgerRow` не существует).

- [ ] **Step 3: Реализовать `LedgerRow`**

Создать `src/AFK4.Operator.App.Web/src/players/LedgerRow.tsx`:

```tsx
import { Undo2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Money } from '../operatorPrimitives';
import type { LedgerEntryView } from './playersModel';

export function LedgerRow({
  view,
  currencyCode,
  compact = false,
  canRefund = false,
  onRefund
}: {
  view: LedgerEntryView;
  currencyCode: string;
  compact?: boolean;
  canRefund?: boolean;
  onRefund?: () => void;
}) {
  const { t } = useI18n();
  const detail = [view.description, view.reason].filter(Boolean).join(' · ');
  const showRefund = !compact && canRefund && !view.isReversal && Boolean(onRefund);
  return (
    <div className={`ui-ledger-row${compact ? ' ui-ledger-row--compact' : ''} ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
      <span className="ui-ledger-time">{view.timeLabel}</span>
      <div className="ui-ledger-body">
        <span className="ui-ledger-title">
          {view.typeLabel}
          {view.isReversal && <em className="ui-ledger-reversal">{t('op.players.history.reversalBadge')}</em>}
        </span>
        {!compact && detail && <span className="ui-ledger-detail">{detail}</span>}
      </div>
      <div className="ui-ledger-aside">
        <Money minorUnits={view.amountMinorUnits} currencyCode={view.currencyCode || currencyCode} signed />
        {showRefund && (
          <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm ui-ledger-refund" onClick={onRefund}>
            <Undo2 size={13} aria-hidden="true" />
            {t('op.players.refund.rowBtn')}
          </button>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `bun test src/players/LedgerRow.test.tsx`
Expected: PASS (4 теста).

- [ ] **Step 5: Сборка (тайпчек)**

Run: `bun run build`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/LedgerRow.tsx src/AFK4.Operator.App.Web/src/players/LedgerRow.test.tsx
git commit -m "feat(operator-ui): компонент LedgerRow — единая строка операции (полная/компактная)"
```

---

## S1 — Клиенты: переезд на `.ui-*` + UX-фиксы §7

> Стратегия каждой задачи: (1) поменять `className` компонента на атомы; (2) применить относящийся UX-фикс; (3) удалить осиротевшие правила из `12-players.css`; (4) обновить/добавить тест; (5) `bun test` затронутого файла. Финальный `bun run build` + полный `bun test` — в Task S1.6.

### Task S1.1: `ClientList` — атомы + UX-фикс §7.1 (одно число + бейдж долга)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientList.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (правила `.clients-segment-chip`, `.client-row*` — упростить/удалить дубли)
- Test: `src/AFK4.Operator.App.Web/src/players/ClientList.test.tsx` (создать, если нет)

**Interfaces:**
- Consumes: `Money` (S0.2); `.ui-chip--filter`, `.ui-chip--status --xs --is-danger`, `.ui-card --interactive --edge` (S0.1); существующий `formatMinorUnits`, `playerStatusLabel`, ключ `op.players.chip.debt`.

- [ ] **Step 1: Фильтр-чипы → `.ui-chip--filter`**

В `ClientList.tsx` заменить кнопку сегмента:

```tsx
// было
<button
  key={segment.id}
  type="button"
  className={`clients-segment-chip${activeSegment === segment.id ? ' active' : ''}`}
  onClick={() => onSelectSegment(segment.id)}
>
  {segment.label}
  <b>{segment.count}</b>
</button>
// стало
<button
  key={segment.id}
  type="button"
  className={`ui-chip ui-chip--filter${activeSegment === segment.id ? ' is-active' : ''}`}
  onClick={() => onSelectSegment(segment.id)}
>
  {segment.label}
  <b>{segment.count}</b>
</button>
```

- [ ] **Step 2: Карточка клиента → `.ui-card` + бейдж долга + `Money`**

Добавить импорт в начало `ClientList.tsx`:

```tsx
import { Money } from '../operatorPrimitives';
```

Заменить тело карточки клиента:

```tsx
// было
<button
  key={client.playerAccountId ?? client.name}
  type="button"
  className={`client-row ${client.tone}${client.status === 'inactive' ? ' is-inactive' : ''}${client.playerAccountId === selectedClientId ? ' selected' : ''}`}
  onClick={() => onSelectClient(client.playerAccountId ?? null)}
>
  <div className="client-row-info">
    <strong className="client-row-name">
      <span className="client-row-name-text">{client.name}</span>
      {client.status !== 'active' && (
        <span className={`client-row-badge is-${client.status}`}>{playerStatusLabel(client.status, t)}</span>
      )}
    </strong>
    <em className="client-row-detail">{client.detail}</em>
  </div>
  <div className="client-row-figures">
    <b className="client-row-balance">{formatMinorUnits(client.balanceMinorUnits, currencyCode)}</b>
    {client.debtMinorUnits > 0 && (
      <small className="client-row-debt">{formatMinorUnits(client.debtMinorUnits, currencyCode)}</small>
    )}
  </div>
</button>
// стало
<button
  key={client.playerAccountId ?? client.name}
  type="button"
  className={`ui-card ui-card--interactive ui-card--edge client-row ${client.tone}${client.status === 'inactive' ? ' is-inactive' : ''}${client.playerAccountId === selectedClientId ? ' selected' : ''}`}
  onClick={() => onSelectClient(client.playerAccountId ?? null)}
>
  <div className="client-row-info">
    <strong className="client-row-name">
      <span className="client-row-name-text">{client.name}</span>
      {client.status !== 'active' && (
        <span className="ui-chip ui-chip--status ui-chip--xs is-neutral">{playerStatusLabel(client.status, t)}</span>
      )}
      {client.debtMinorUnits > 0 && (
        <span className="ui-chip ui-chip--status ui-chip--xs is-danger">
          {t('op.players.chip.debt')} {formatMinorUnits(client.debtMinorUnits, currencyCode)}
        </span>
      )}
    </strong>
    <em className="client-row-detail">{client.detail}</em>
  </div>
  <div className="client-row-figures">
    <Money minorUnits={client.balanceMinorUnits} currencyCode={currencyCode} />
  </div>
</button>
```

- [ ] **Step 3: Упростить `12-players.css` — `.client-row` (только раскладка + кант), удалить `.client-row-badge`/`.client-row-debt`/`.client-row-balance`, `.clients-segment-chip`**

Найти правило `.client-row` в `src/styles/12-players.css`. Заменить его визуальные свойства (border/background/radius/hover уходят в `.ui-card`), оставив раскладку и кант через `--edge`:

```css
/* было (пример) */
.client-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-1) var(--space-2);
  min-height: var(--control-lg);
  border: 1px solid var(--border-default);
  border-left: 3px solid var(--text-tertiary);
  border-radius: 6px;
  padding: 5px 9px;
  background: var(--surface-card);
  transition: border-color 120ms ease, background 120ms ease, transform 120ms ease;
}
.client-row:hover { border-color: var(--border-accent); background: var(--surface-hover); }
/* стало */
.client-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--space-1) var(--space-2);
  min-height: var(--control-lg);
  padding: var(--space-1) var(--space-2);
  text-align: left;
  --edge: var(--text-tertiary);
}
```

Правила тонирования канта — привести к переменной `--edge` (заменить `border-left-color` на `--edge`):

```css
/* было */
.client-row.active { border-left-color: var(--success); }
.client-row.debt { border-left-color: var(--danger); }
/* стало */
.client-row.active { --edge: var(--success); }
.client-row.debt { --edge: var(--danger); }
```

Оставить правило выбранной строки (селект-кольцо):

```css
.client-row.selected {
  border-color: var(--border-accent);
  background: var(--surface-hover);
  box-shadow: inset 0 0 0 1px rgba(var(--accent-rgb), 0.22);
}
```

Удалить полностью осиротевшие правила (их элементы больше не рендерятся или заменены атомами): `.client-row-badge`, `.client-row-badge.is-debt`, `.client-row-badge.is-inactive`, `.client-row-debt`, `.client-row-balance`, `.clients-segment-chip`, `.clients-segment-chip.active`, `.client-row:hover` (перенесён в `.ui-card--interactive`). `.client-row-figures`, `.client-row-info`, `.client-row-name`, `.client-row-name-text`, `.client-row-detail` — **оставить** (раскладка).

- [ ] **Step 4: Тест — карточка показывает одно число справа + бейдж долга**

Создать `src/AFK4.Operator.App.Web/src/players/ClientList.test.tsx` (если файла нет). Если есть — добавить блок `describe('debt row')`:

```tsx
import { describe, expect, it, afterEach } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientList } from './ClientList';
import type { PlayerClientItem } from '../operatorHelpers';

afterEach(cleanup);

const debtor: PlayerClientItem = {
  playerAccountId: 'p1', name: 'Мадина Саидова', status: 'active',
  balanceMinorUnits: 0, debtMinorUnits: 3500, last: '', tone: 'debt',
  detail: '+992 98 700 11 22 · 0 пакетов', phoneNumber: '+992987001122', source: 'backend'
};

const renderList = () =>
  render(
    <I18nProvider initialLocale="ru">
      <ClientList
        clients={[debtor]}
        currencyCode="TJS"
        search=""
        segments={[]}
        activeSegment="all"
        selectedClientId={null}
        showSkeleton={false}
        canCreatePlayer={false}
        emptyDescription=""
        onSearchChange={() => {}}
        onSelectSegment={() => {}}
        onSelectClient={() => {}}
        onNewClient={() => {}}
      />
    </I18nProvider>
  );

describe('ClientList debtor row', () => {
  it('shows a single balance figure and a debt badge (no stacked second number)', () => {
    renderList();
    // баланс — одно число справа (mono)
    expect(screen.getByText('0 с.')).toHaveClass('ui-money');
    // долг — бейдж «Долг 35 с.» рядом с именем
    expect(screen.getByText(/Долг\s+35\s+с\./)).toHaveClass('ui-chip--status');
  });
});
```

> Примечание: сверить фактический интерфейс пропсов `ClientList` в `ClientList.tsx` (шапка `function ClientList({...})`) и при расхождении привести список пропсов теста в соответствие. Значения выше — минимально необходимые для рендера строки.

- [ ] **Step 5: Прогнать тест**

Run: `bun test src/players/ClientList.test.tsx`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientList.tsx src/AFK4.Operator.App.Web/src/players/ClientList.test.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-clients): ClientList на .ui-* + одно число справа, долг бейджем (§7.1)"
```

---

### Task S1.2: `HistorySection` + `ClientLedgerRail` — `LedgerRow` + hover-«Вернуть» (§7.2)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/HistorySection.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (удалить `.client-history-row/-time/-body/-detail/-reversal/-amount/-refund`; фильтры → `.ui-chip--filter`; `.clients-history-more` → атом кнопки)
- Test: `src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx` (создать/дополнить)

> `ClientLedgerRail.tsx` рендерит `<HistorySection>` внутри — отдельных правок разметки не требует; проверяется в S1.6.

**Interfaces:**
- Consumes: `LedgerRow` (S0.3), `projectLedgerEntry` (существует в `playersModel`), `.ui-chip--filter`, `.ui-ledger-list`, `.ui-btn` (S0.1).

- [ ] **Step 1: Фильтры → `.ui-chip--filter`**

В `HistorySection.tsx` заменить оба места класса `clients-history-filter${active?' active'}` на `ui-chip ui-chip--filter${active?' is-active'}` (кнопка «Все» и `HISTORY_FILTER_TYPES.map`).

- [ ] **Step 2: Список операций → `LedgerRow`**

Добавить импорт:

```tsx
import { LedgerRow } from './LedgerRow';
```

Заменить блок `.clients-history-list` с ручным рендером `<article className="client-history-row">…`:

```tsx
// было
<div className="clients-history-list">
  {entries.map((raw) => {
    const view = projectLedgerEntry(raw, t);
    const sign = view.isCredit ? '+' : '−';
    const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);
    return (
      <article key={view.id} className={`client-history-row ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
        <span className="client-history-time">{view.timeLabel}</span>
        <div className="client-history-body">
          <strong>
            {view.typeLabel}
            {view.isReversal && <em className="client-history-reversal">{t('op.players.history.reversalBadge')}</em>}
          </strong>
          {(view.description || view.reason) && (
            <span className="client-history-detail">
              {[view.description, view.reason].filter(Boolean).join(' · ')}
            </span>
          )}
        </div>
        <div className="client-history-aside">
          <b className="client-history-amount">{sign}{amount}</b>
          {canRefund && !view.isReversal && (
            <button type="button" className="client-history-refund" onClick={() => onRefund(raw)}>
              <Undo2 size={13} aria-hidden="true" />
              {t('op.players.refund.rowBtn')}
            </button>
          )}
        </div>
      </article>
    );
  })}
</div>
// стало
<div className="clients-history-list ui-ledger-list">
  {entries.map((raw) => {
    const view = projectLedgerEntry(raw, t);
    return (
      <LedgerRow
        key={view.id}
        view={view}
        currencyCode={currencyCode}
        canRefund={canRefund}
        onRefund={() => onRefund(raw)}
      />
    );
  })}
</div>
```

> После замены проверить, что импорты `Undo2` и `formatMinorUnits` ещё используются в файле; если больше нет — удалить неиспользуемые импорты (иначе `bun run build`/`tsc` предупредит).

- [ ] **Step 3: Кнопка «Загрузить ещё» → атом**

```tsx
// было
<button type="button" className="clients-history-more" disabled={loading} onClick={onLoadMore}>
  <RefreshCw size={14} aria-hidden="true" />{t('op.players.history.loadMore')}
</button>
// стало
<button type="button" className="ui-btn ui-btn--block clients-history-more" disabled={loading} onClick={onLoadMore}>
  <RefreshCw size={14} aria-hidden="true" />{t('op.players.history.loadMore')}
</button>
```

- [ ] **Step 4: `12-players.css` — удалить осиротевшие правила**

Удалить: `.client-history-row`, `.client-history-row.is-credit/.is-debit`, `.client-history-time`, `.client-history-body`, `.client-history-body strong`, `.client-history-detail`, `.client-history-reversal`, `.client-history-aside`, `.client-history-amount`, `.client-history-refund` (и его состояния). Правила фильтров `.clients-history-filter`/`.active` — удалить (заменены `.ui-chip--filter`). `.clients-history-list` — оставить (контейнер, теперь с `.ui-ledger-list`). Визуальные свойства `.clients-history-more` (border/bg) — удалить, оставить только уникальную раскладку если есть (напр. `margin-top`); иначе удалить правило целиком.

- [ ] **Step 5: Тест — «Вернуть» скрыт по умолчанию (opacity), проявляется по фокусу; сторно без «Вернуть»**

Создать/дополнить `src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx`:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { HistorySection } from './HistorySection';
import type { LedgerEntryDto } from '../api/clients/players';

afterEach(cleanup);

const topUp: LedgerEntryDto = {
  ledgerEntryId: 'le-1', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
  description: 'Пополнение кошелька', reason: 'Касса', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-23T04:00:00Z'
};

const renderHistory = (over = {}) =>
  render(
    <I18nProvider initialLocale="ru">
      <HistorySection
        entries={[topUp]}
        currencyCode="TJS"
        activeFilter={null}
        onFilterChange={() => {}}
        hasMore={false}
        onLoadMore={() => {}}
        loading={false}
        canRefund
        onRefund={() => {}}
        {...over}
      />
    </I18nProvider>
  );

describe('HistorySection', () => {
  it('renders a refund action inside a ledger row for a refundable entry', () => {
    const onRefund = mock(() => {});
    renderHistory({ onRefund });
    fireEvent.click(screen.getByRole('button', { name: /Вернуть/ }));
    expect(onRefund).toHaveBeenCalled();
  });

  it('does not render refund when caller has no permission', () => {
    renderHistory({ canRefund: false });
    expect(screen.queryByRole('button', { name: /Вернуть/ })).toBeNull();
  });
});
```

> Примечание: hover-«проявление» — чисто CSS (`opacity`), в happy-dom не проверяется; поведенческая гарантия здесь — что действие существует/кликается и гейтится по `canRefund`/`isReversal` (последнее покрыто в `LedgerRow.test.tsx`). Свериться с фактическими пропсами `HistorySection`.

- [ ] **Step 6: Прогнать тест + sanity истории**

Run: `bun test src/players/HistorySection.test.tsx`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/HistorySection.tsx src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-clients): история на LedgerRow + «Вернуть» по hover, фильтры на .ui-chip (§7.2)"
```

---

### Task S1.3: `WalletSection` — атомы, мини-лента на `LedgerRow`, placeholders (§7.5) и иерархия кнопок (§7.6)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/WalletSection.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx` (дефолты полей — строки 53–56)
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css`
- Test: `src/AFK4.Operator.App.Web/src/players/WalletSection.test.tsx` (существует — дополнить)

**Interfaces:**
- Consumes: `LedgerRow` (S0.3), `projectLedgerEntry`, `.ui-field`, `.ui-btn --primary/--danger/--ghost/--block`, `.ui-ledger-list` (S0.1). Ключ-плейсхолдер причины — существующий `op.players.actions.topUpDefault` / `op.players.actions.writeOffDebtDefault`.

- [ ] **Step 1: Поля → `.ui-field` + placeholder суммы «0.00» и причины (существующий дефолт как placeholder)**

Импорт:

```tsx
import { LedgerRow } from './LedgerRow';
```

Форма пополнения — обёртки полей и инпуты:

```tsx
// было
<div className="clients-wallet-fields">
  <div className="clients-wallet-field">
    <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
    <input id="wallet-topup-amount" inputMode="decimal" value={topUpAmount} disabled={!canTopUp}
      onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)} />
  </div>
  <div className="clients-wallet-field">
    <label htmlFor="wallet-topup-reason">{t('op.players.actions.topUpReasonLabel')}</label>
    <input id="wallet-topup-reason" value={topUpReason} disabled={!canTopUp}
      onChange={(event) => onChangeTopUpReason(event.currentTarget.value)} />
  </div>
</div>
// стало
<div className="clients-wallet-fields">
  <div className="ui-field">
    <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
    <input id="wallet-topup-amount" inputMode="decimal" placeholder="0.00" value={topUpAmount} disabled={!canTopUp}
      onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)} />
  </div>
  <div className="ui-field">
    <label htmlFor="wallet-topup-reason">{t('op.players.actions.topUpReasonLabel')}</label>
    <input id="wallet-topup-reason" placeholder={t('op.players.actions.topUpDefault')} value={topUpReason} disabled={!canTopUp}
      onChange={(event) => onChangeTopUpReason(event.currentTarget.value)} />
  </div>
</div>
```

Аналогично для формы долга: обёртки `clients-wallet-field` → `ui-field`; на инпут суммы добавить `placeholder="0.00"`; на инпут причины `placeholder={t('op.players.actions.writeOffDebtDefault')}`.

- [ ] **Step 2: Кнопки — иерархия (§7.6)**

```tsx
// пополнение: было
<button type="submit" className="clients-primary-action" disabled={!canTopUp}>
// стало
<button type="submit" className="ui-btn ui-btn--primary ui-btn--block" disabled={!canTopUp}>

// долг: было
<button type="submit" className="clients-primary-action clients-debt-action" disabled={!canPayDebt}>
// стало
<button type="submit" className="ui-btn ui-btn--danger ui-btn--block" disabled={!canPayDebt}>

// коррекция: было
<button type="button" className="clients-wallet-correction-link" onClick={onCorrect}>
// стало (тихая ghost-ссылка, НЕ во всю ширину)
<button type="button" className="ui-btn ui-btn--ghost ui-btn--sm" onClick={onCorrect}>
```

- [ ] **Step 3: Мини-лента недавних → `LedgerRow` (компактный)**

```tsx
// было
<ul className="clients-wallet-recent-list">
  {recentEntries.map((raw) => {
    const view = projectLedgerEntry(raw, t);
    const sign = view.isCredit ? '+' : '−';
    const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);
    return (
      <li key={view.id} className={`clients-wallet-recent-row ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
        <span className="clients-wallet-recent-time">{view.timeLabel}</span>
        <span className="clients-wallet-recent-type">{view.typeLabel}</span>
        <b className="clients-wallet-recent-amount">{sign}{amount}</b>
      </li>
    );
  })}
</ul>
// стало
<div className="clients-wallet-recent-list ui-ledger-list">
  {recentEntries.map((raw) => {
    const view = projectLedgerEntry(raw, t);
    return <LedgerRow key={view.id} view={view} currencyCode={currencyCode} compact />;
  })}
</div>
```

> После замены проверить/удалить ставшие ненужными импорты (`formatMinorUnits`, если больше не используется в файле).

- [ ] **Step 4: Дефолты полей — пустые (§7.5), причина уходит в placeholder**

В `BackendPlayersWorkspace.tsx` строки 53–56:

```tsx
// было
const [walletTopUpAmount, setWalletTopUpAmount] = useState('100.00');
const [walletTopUpReason, setWalletTopUpReason] = useState(() => t('op.players.actions.topUpDefault'));
const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
const [debtPaymentReason, setDebtPaymentReason] = useState(() => t('op.players.actions.writeOffDebtDefault'));
// стало
const [walletTopUpAmount, setWalletTopUpAmount] = useState('');
const [walletTopUpReason, setWalletTopUpReason] = useState('');
const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
const [debtPaymentReason, setDebtPaymentReason] = useState('');
```

> **Проверка побочек:** причина теперь пустая по умолчанию. Убедиться, что отправка пополнения/погашения не требует непустой причины на клиенте (искать в `BackendPlayersWorkspace.tsx` условие `canTopUp`/обработчик `onTopUp`, где формируется reason). Если причина строго обязательна и пустая ломает поток — причина является опциональной метаданностью кассовой операции; ослабить до опциональной (не блокировать сабмит на пустой причине). Задокументировать выбор в сообщении коммита, если правка потребовалась.

- [ ] **Step 5: `12-players.css` — чистка**

Удалить: `.clients-wallet-field` (label/input визуал — теперь `.ui-field`), `.clients-wallet-form input` (стили инпута — в `.ui-field`), `.clients-primary-action`, `.clients-debt-action`, `.clients-wallet-correction-link`, `.clients-wallet-recent-row` (+ is-credit/is-debit), `.clients-wallet-recent-time/-type/-amount`. Оставить: `.clients-wallet-layout`, `.clients-wallet-actions`, `.clients-wallet-form` (+ `-debt` — рамка-акцент формы долга), `.clients-wallet-fields` (грид полей), `.clients-wallet-recent` (панель), `.clients-wallet-recent-head`, `.clients-wallet-recent-list` (контейнер), `.clients-wallet-recent-link`, `.clients-section-title`.

- [ ] **Step 6: Обновить существующий `WalletSection.test.tsx`**

Существующий тест передаёт `topUpAmount="100.00"` и `topUpReason="пополнение через кассу"` как пропсы — эти кейсы остаются валидны (компонент рендерит value как есть). Добавить кейс на placeholder пустого поля:

```tsx
it('shows a 0.00 placeholder for an empty top-up amount', () => {
  renderSection({ topUpAmount: '' });
  const input = screen.getByLabelText('Сумма пополнения') as HTMLInputElement;
  expect(input).toHaveAttribute('placeholder', '0.00');
  expect(input.value).toBe('');
});
```

- [ ] **Step 7: Прогнать тест**

Run: `bun test src/players/WalletSection.test.tsx`
Expected: PASS (существующие + новый).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/WalletSection.tsx src/AFK4.Operator.App.Web/src/players/WalletSection.test.tsx src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-clients): кошелёк на .ui-field/.ui-btn + LedgerRow, пустые поля с placeholder (§7.5/§7.6)"
```

---

### Task S1.4: `ClientDetail` + `ClientContextStrip` — стат-карточки, дубль «Пакеты» (§7.4), Money, контекст-чипы

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientContextStrip.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css`
- Test: `src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx` (создать/дополнить)

**Interfaces:**
- Consumes: `Money` (S0.2); `.ui-card--stat`, `.ui-chip--status --is-live/--is-booking`, `.ui-chip--filter`/`--xs` (S0.1); `props.packageCount` (уже есть в `ClientDetail`), `props.balanceMinorUnits`, `props.debtMinorUnits`.

- [ ] **Step 1: Стат-карточки → `.ui-card--stat` + `Money`; убрать карточку «Пакеты» (§7.4)**

Импорт:

```tsx
import { Money } from '../operatorPrimitives';
```

```tsx
// было
<div className="client-detail-chips">
  <div className="client-chip">
    <span>{t('op.players.chip.balance')}</span>
    <strong>{formatMinorUnits(props.balanceMinorUnits, props.currencyCode)}</strong>
  </div>
  <div className={`client-chip${hasDebt ? ' is-debt' : ''}`}>
    <span>{t('op.players.chip.debt')}</span>
    <strong>{formatMinorUnits(props.debtMinorUnits, props.currencyCode)}</strong>
  </div>
  <div className="client-chip">
    <span>{t('op.players.chip.packages')}</span>
    <strong>{props.packageCount}</strong>
  </div>
</div>
// стало (две карточки; счётчик пакетов уезжает на вкладку — Step 2)
<div className="client-detail-chips">
  <div className="ui-card ui-card--stat">
    <span>{t('op.players.chip.balance')}</span>
    <strong><Money minorUnits={props.balanceMinorUnits} currencyCode={props.currencyCode} /></strong>
  </div>
  <div className={`ui-card ui-card--stat${hasDebt ? ' is-danger' : ''}`}>
    <span>{t('op.players.chip.debt')}</span>
    <strong><Money minorUnits={props.debtMinorUnits} currencyCode={props.currencyCode} /></strong>
  </div>
</div>
```

- [ ] **Step 2: Табы → `.ui-chip--filter`; счётчик пакетов бейджем на вкладке «Пакеты» (§7.4)**

```tsx
// было
<div className="client-detail-tabs" role="tablist">
  {tabs.map((tab) => (
    <button
      key={tab.id}
      type="button"
      role="tab"
      aria-selected={activeTab === tab.id}
      className={`client-detail-tab${activeTab === tab.id ? ' active' : ''}`}
      onClick={() => props.onSelectTab(tab.id)}
    >
      {tab.label}
    </button>
  ))}
</div>
// стало
<div className="client-detail-tabs" role="tablist">
  {tabs.map((tab) => (
    <button
      key={tab.id}
      type="button"
      role="tab"
      aria-selected={activeTab === tab.id}
      className={`client-detail-tab${activeTab === tab.id ? ' active' : ''}`}
      onClick={() => props.onSelectTab(tab.id)}
    >
      {tab.label}
      {tab.id === 'packages' && props.packageCount > 0 && (
        <span className="ui-chip ui-chip--status ui-chip--xs is-neutral client-tab-count">{props.packageCount}</span>
      )}
    </button>
  ))}
</div>
```

> `.client-detail-tab` — оставить (это подчёркивание-таб, не pill); бейдж `.client-tab-count` — добавить в CSS минимальный отступ (`margin-left: var(--space-2)`).

- [ ] **Step 3: `ClientContextStrip` — статус-чипы → `.ui-chip--status`**

```tsx
// было
<span className="client-context-chip is-live"> … </span>
<span className="client-context-chip is-booking"> … </span>
// стало
<span className="ui-chip ui-chip--status is-live"> … </span>
<span className="ui-chip ui-chip--status is-booking"> … </span>
```

- [ ] **Step 4: Кнопка «Бронь» → атом**

База `.ui-btn` уже нейтральная вторичная — отдельного `--secondary` не нужно:

```tsx
// было
<button type="button" className="client-detail-reservation" disabled={!props.canCreateReservation} onClick={props.onCreateReservation}>
// стало
<button type="button" className="ui-btn" disabled={!props.canCreateReservation} onClick={props.onCreateReservation}>
```

> `client-detail-reservation` убрать, если в `12-players.css` у него нет уникальной раскладки (только визуал border/bg — он теперь в `.ui-btn`); оставить класс лишь при наличии уникального позиционирования.

- [ ] **Step 5: `12-players.css` — чистка**

Удалить/упростить: `.client-chip` (+ `.is-debt`, `span`, `strong`) → удалить (заменены `.ui-card--stat`). `.client-context-chip` (+ `.is-live/.is-booking`) → удалить (заменены `.ui-chip--status`). `.client-detail-reservation` (border/bg) → удалить визуал (атом), оставить только если есть уникальная раскладка. `.client-detail-tab`/`.active` — оставить (таб-андерлайн). `.client-detail-chips` (грид карточек) — оставить. Добавить:

```css
.client-tab-count { margin-left: var(--space-2); }
```

- [ ] **Step 6: Тест — две стат-карточки, счётчик пакетов на вкладке**

Создать/дополнить `src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx`:

```tsx
it('shows two money stat cards and package count on the packages tab (no packages stat card)', () => {
  renderDetail({ balanceMinorUnits: 45000, debtMinorUnits: 3500, packageCount: 2 });
  // деньги — mono
  expect(screen.getByText('450 с.')).toHaveClass('ui-money');
  // счётчик пакетов на вкладке, а не отдельной стат-карточкой
  const packagesTab = screen.getByRole('tab', { name: /Пакеты/ });
  expect(packagesTab).toHaveTextContent('2');
});
```

> Хелпер `renderDetail` собрать по образцу `WalletSection.test.tsx` (обёртка `I18nProvider`), заполнив обязательные пропсы `ClientDetail` из его сигнатуры. Свериться с фактическим списком пропсов в `ClientDetail.tsx`.

- [ ] **Step 7: Прогнать тест**

Run: `bun test src/players/ClientDetail.test.tsx`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx src/AFK4.Operator.App.Web/src/players/ClientContextStrip.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-clients): профиль на .ui-card--stat/.ui-chip + счётчик пакетов на вкладку (§7.4)"
```

---

### Task S1.5: `PackagesSection` — атомы + `Money`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/PackagesSection.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css`
- Test: `src/AFK4.Operator.App.Web/src/players/PackagesSection.test.tsx` (создать/дополнить)

**Interfaces:**
- Consumes: `Money` (S0.2); `.ui-card --edge` (строка пакета), `.ui-field` (select), `.ui-btn --primary --block` (кнопка покупки) (S0.1).

- [ ] **Step 1: Строка пакета → `.ui-card`; цены → `Money`**

Импорт `Money`. Заменить обёртку строки пакета:

```tsx
// было
<article key={view.id} className={`client-package-row${view.isExpired ? ' is-expired' : ''}`}>
// стало
<article key={view.id} className={`ui-card client-package-row${view.isExpired ? ' is-expired' : ''}`}>
```

Заменить денежные значения превью на `Money`:

```tsx
// было
<b>{formatMinorUnits(priceMinorUnits, optionCurrency)}</b>
// стало
<b><Money minorUnits={priceMinorUnits} currencyCode={optionCurrency} /></b>
```

И в блоке депозита (`depositLow`) — сумму нехватки через `Money` внутри строки (оставить существующий `t(...)` c плейсхолдером `amount`, но передать в него `formatMinorUnits` как сейчас — этот путь оставить без изменений, т.к. это интерполяция в строку перевода, `Money` там неприменим).

- [ ] **Step 2: Select покупки → `.ui-field`; кнопка → `.ui-btn --primary`**

```tsx
// было
<label className="clients-package-select"> … <select …>…</select></label>
// стало — обернуть в .ui-field (label сверху)
<label className="ui-field clients-package-select"> … <select …>…</select></label>

// было
<button type="button" className="clients-primary-action" disabled={!canPurchase || busy || !canAfford} onClick={onBuy}>
// стало
<button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={!canPurchase || busy || !canAfford} onClick={onBuy}>
```

- [ ] **Step 3: `12-players.css` — чистка**

Удалить визуал `.client-package-row` (border/bg/radius/padding — теперь `.ui-card`), оставив уникальную раскладку (грид колонок) и `.is-expired { opacity: .6 }`. `.clients-package-select` — если селект-стили дублируют `.ui-field > select`, удалить дубль (оставить только уникальную раскладку label). Удалить `.clients-primary-action`-специфику для пакета (общий класс уже удалён в S1.3).

- [ ] **Step 4: Тест — цена пакета mono; кнопка покупки — primary**

Создать/дополнить `src/AFK4.Operator.App.Web/src/players/PackagesSection.test.tsx`:

```tsx
it('renders package price as mono money and a primary buy button', () => {
  renderPackages(/* с одной опцией, canPurchase, canAfford */);
  expect(screen.getByRole('button', { name: /Купить/ })).toHaveClass('ui-btn--primary');
});
```

> Хелпер `renderPackages` собрать по образцу; свериться с сигнатурой `PackagesSection`. Имя кнопки — по фактическому ключу `op.players.actions.buyPackageBtn`.

- [ ] **Step 5: Прогнать тест**

Run: `bun test src/players/PackagesSection.test.tsx`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/PackagesSection.tsx src/AFK4.Operator.App.Web/src/players/PackagesSection.test.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-clients): пакеты на .ui-card/.ui-field/.ui-btn + Money"
```

---

### Task S1.6: Финальная чистка `12-players.css` + гейт слайса

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (финальный проход)
- Проверка: `src/AFK4.Operator.App.Web/src/players/ClientLedgerRail.tsx` (переезд не требует правок — рендерит `HistorySection`)

**Interfaces:**
- Consumes: всё из S0/S1.

- [ ] **Step 1: §7.3 — верификация (без изменений)**

Открыть `BackendPlayersWorkspace.tsx:733-737`. Убедиться, что счётчик «Долги» использует `tone={overview.debtMinorUnits > 0 ? 'warning' : undefined}` (амбер-точка `--warning`, НЕ зелёная). Это уже корректно — **никаких правок**. Миграция `StateFlag → .ui-chip--count` отложена в отдельный кросс-секционный слайс (StateFlag используется также в Карте/Кассе — трогать сейчас = выйти за scope S1).

- [ ] **Step 2: Прогон grep — не осталось ли мёртвых `.clients-*`/`.client-*` правил, чьи элементы удалены**

Run (из `src/AFK4.Operator.App.Web/src/styles`):
`rg -n "client-row-badge|client-row-debt|client-row-balance|clients-segment-chip|client-history-row|client-history-amount|client-history-refund|clients-history-filter|clients-wallet-recent-row|clients-wallet-field|clients-primary-action|clients-debt-action|clients-wallet-correction-link|client-chip|client-context-chip" 12-players.css`
Expected: пусто (или только легитимные контейнеры, которые решено оставить). Удалить оставшиеся мёртвые правила.

- [ ] **Step 3: Полный прогон тестов раздела + App.test**

Run (из `src/AFK4.Operator.App.Web`): `bun test`
Expected: PASS (все, включая `App.test` отдельным прогоном, если требуется — запустить `bun test src/App.test.tsx` дополнительно).

- [ ] **Step 4: Сборка (гейт слайса)**

Run: `bun run build`
Expected: PASS (`tsc -b && vite build` — тайпчек тестов и сужений зелёный).

- [ ] **Step 5: Живой визуальный контроль (mock)**

Запустить dev-превью (`bun run dev`, mock по умолчанию) и глазами проверить экран «Клиенты»: строки списка одной высоты, у должника одно число справа + бейдж долга; фильтры/чипы единого вида; «Вернуть» проявляется на hover строки; поля пополнения пусты с placeholder «0.00»; деньги моноширинные; две стат-карточки; счётчик пакетов на вкладке. (Ссылку на превью отдать пользователю — не headless-скриншоты.)

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "chore(operator-clients): финальная чистка мёртвого CSS раздела + гейт слайса S1"
```

---

## Self-Review (проведён при написании плана)

**Покрытие спеки:**
- §4.1 файл `02-ui-kit.css` + подключение → S0.1 ✓
- §5.1 `.ui-btn` → S0.1 ✓ (применён в S1.3/S1.4/S1.5)
- §5.2 `.ui-chip` (filter/status/count/xs) → S0.1 ✓ (S1.1 filter+xs, S1.4 status, §7.3 count отложен явно)
- §5.3 `.ui-card` (interactive/edge/stat) → S0.1 ✓ (S1.1 карточка, S1.4 stat, S1.5 package)
- §5.4 `.ui-field` → S0.1 ✓ (S1.3 кошелёк, S1.5 select)
- §6.1 `Money` → S0.2 ✓
- §6.2 `LedgerRow` → S0.3 ✓ (S1.2 история/рейл, S1.3 мини-лента)
- §7.1 строка долга → S1.1 ✓
- §7.2 «Вернуть» hover → S0.1 (CSS) + S0.3 + S1.2 ✓
- §7.3 счётчик «Долги» → S1.6 Step 1: **уже корректно (warning), фейковый фикс не вносится** ✓ (честная поправка к спеке — см. ниже)
- §7.4 дубль «Пакеты» → S1.4 ✓
- §7.5 пустые поля + placeholder → S1.3 ✓
- §7.6 иерархия кнопок → S1.3 ✓
- §8 дисциплина шкал → Global Constraints + атомы на токенах ✓
- §9 гейт слайса (bun test + build) → S1.6 + per-task ✓
- §10 тесты/паритет → per-task тесты + S1.6 Step 3–5 ✓

**Поправка к спеке (§7.3):** проверка кода показала, что счётчик «Долги» уже тонируется `warning` (амбер-точка), а не зелёным — «зелёная точка» в §7.3 была мисридом скриншота. План не вносит фейковый фикс; §7.3 сводится к верификации. Спеку §7.3 следует поправить (пометить как «уже верно»).

**Плейсхолдеры:** новый код (атомы, `Money`, `LedgerRow`) приведён полностью; миграции — точные before→after блоки. Места «свериться с фактической сигнатурой пропсов» относятся к тест-хелперам (пропсы компонентов длинные и не извлекались целиком) — это указание проверить, а не заглушка в продакшн-коде.

**Согласованность типов:** `Money({minorUnits, currencyCode, signed})`, `LedgerRow({view, currencyCode, compact, canRefund, onRefund})`, `LedgerEntryView` (из `playersModel`), `PlayerClientItem` (из `operatorHelpers`), `LedgerEntryDto` (из `api/clients/players`) — использованы единообразно во всех задачах и тестах.
