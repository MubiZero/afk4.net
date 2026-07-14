# Operator «Склад»: сайдбар-герои + стандартизация сканера — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить плоскую правую панель «Сводка» на всех 4 вкладках Склада (Остатки/Приёмка/Журнал/Инвентаризация) на единую панель «герой + hairline-секции + CTA» (язык Кассы/Карты/Брони), и привести строку сканера/поиска в Приёмке и Инвентаризации к одному эталонному виду (язык Кассы/POS).

**Architecture:** Два новых переиспользуемых презентационных компонента в `src/AFK4.Operator.App.Web/src/stock/` — `StockHero.tsx` (цветной герой-показатель) и `ScanSearchBar.tsx` (строка поиска+бейдж сканера, поверх нового общего ui-kit атома). Четыре существующих workspace-компонента переписывают только JSX своих `<aside className="stock-summary">` (и, где есть, полосы сканера), логика/данные не меняются. Общие атомы `.ui-search-field`/`.ui-scanner-badge` переезжают из `11-pos.css` в `02-ui-kit.css`, POS мигрирует на них первым — так к моменту переиспользования в Складе атом уже проверен существующими POS-тестами.

**Tech Stack:** React + TypeScript, bun test + @testing-library/react, CSS без препроцессора (`packages/tokens/tokens.css` переменные), i18n через `@afk4/i18n` (`locales/{ru,en,tg}.json` → `bun run gen`).

## Global Constraints

- Спека: `docs/superpowers/specs/2026-07-08-operator-stock-sidebar-scanner-redesign-design.md`.
- Новых CSS-токенов не заводить — только существующие переменные `packages/tokens/tokens.css`.
- Реализовать на отдельной ветке (не на `feat/operator-clients-center-redesign`) — см. Task 0.
- Финальный гейт каждого таск-коммита: `bun test` в `src/AFK4.Operator.App.Web`; в последнем таске плана — обязательный `bun run build` (правило проекта: `tsc -b` тайпчекает тесты, зелёный `bun test` не гарантирует зелёную сборку).
- i18n: редактировать `locales/{ru,en,tg}.json`, затем `bun run gen` в `packages/i18n` — `src/messages.ts` руками не трогать (auto-generated).
- tg-переводы — не копия ru (guard-тест `tg !== ru`), см. `packages/i18n` тесты.
- Не использовать модель `opus` при делегировании задач субагентам — только sonnet/haiku.

---

## Task 0: Ветка

**Files:** нет изменений кода.

- [ ] **Step 1: Создать и переключиться на ветку от текущей**

```bash
git checkout -b feat/operator-stock-sidebar-scanner-redesign
```

- [ ] **Step 2: Проверить чистоту рабочего дерева**

Run: `git status`
Expected: `nothing to commit, working tree clean` (ветка создана от чистого HEAD).

---

## Task 1: Общие ui-kit атомы `.ui-search-field` / `.ui-scanner-badge` — миграция POS

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css` (добавить атомы)
- Modify: `src/AFK4.Operator.App.Web/src/styles/11-pos.css:441-469,983-1017` (удалить старые правила, добавить локальный модификатор отступа)
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx:371-384` (новые имена классов)
- Test: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx` (существующий, без новых кейсов — проверяет, что рендер не сломан)

**Interfaces:**
- Производит два CSS-атома `.ui-search-field` (иконка+инпут в общей рамке, focus-ring) и `.ui-scanner-badge`/`.ui-scanner-pulse` (пульсирующий бейдж «Сканер активен») — потребляются Task 3 (`ScanSearchBar.tsx`).

- [ ] **Step 1: Запустить существующие тесты POS — зафиксировать зелёную базовую линию**

Run: `cd src/AFK4.Operator.App.Web && bun test src/BackendPosWorkspace.test.tsx`
Expected: PASS (все существующие тесты зелёные, ни один не проверяет `.pos-search`/`.pos-scanner-badge` по имени класса — переименование безопасно).

- [ ] **Step 2: Добавить общие атомы в `02-ui-kit.css`**

Вставить после `.ui-field-error { color: var(--danger-text); font-size: var(--text-xs); }` (текущая строка 243):

```css

/* ============ Search field (иконка + инпут в общей рамке) ============ */
.ui-search-field {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  height: var(--control-md);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  padding: 0 var(--space-3);
  background: var(--surface-sunken);
  color: var(--text-tertiary);
}

.ui-search-field:focus-within {
  border-color: var(--accent);
  box-shadow: var(--focus-ring);
}

.ui-search-field input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--text-primary);
  font-size: var(--text-sm);
}

/* ============ Бейдж активного сканера (пульс) ============ */
.ui-scanner-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 4px 8px;
  border-radius: var(--radius-sm);
  background: var(--surface-accent-soft);
  color: var(--accent-text);
  font-size: 11px;
  font-weight: 500;
  white-space: nowrap;
  flex-shrink: 0;
}

.ui-scanner-pulse {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--accent);
  animation: ui-scanner-pulse 1.4s ease-in-out infinite;
}

@keyframes ui-scanner-pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50%       { opacity: 0.4; transform: scale(0.75); }
}

@media (prefers-reduced-motion: reduce) {
  .ui-scanner-pulse { animation: none; }
}
```

- [ ] **Step 3: Удалить старые правила из `11-pos.css`, оставить локальный модификатор отступа**

Заменить блок (текущие строки 441-469):

```css
/* ── Поиск / поле ввода ── */
.pos-search {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  height: var(--control-md);
  margin: var(--space-3) var(--space-4) var(--space-2);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  padding: 0 var(--space-3);
  background: var(--surface-sunken);
  color: var(--text-tertiary);
}

.pos-search:focus-within {
  border-color: var(--accent);
  box-shadow: var(--focus-ring);
}

.pos-search input {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--text-primary);
  font-size: var(--text-sm);
}
```

на:

```css
/* ── Поиск каталога — чром взят из общего атома .ui-search-field (02-ui-kit.css),
   здесь только отступ размещения внутри панели каталога. ── */
.pos-catalog-search {
  margin: var(--space-3) var(--space-4) var(--space-2);
}
```

Затем удалить блок (текущие строки 983-1017, комментарий + `.pos-scanner-badge`/`.pos-scanner-pulse`/`@keyframes scanner-pulse`/reduced-motion) целиком — атомы теперь в `02-ui-kit.css`.

- [ ] **Step 4: Обновить `BackendPosWorkspace.tsx` на новые имена классов**

Заменить (текущие строки 371-384):

```tsx
              <span className="pos-scanner-badge" aria-label={t('op.pos.scan.active')}>
                <span className="pos-scanner-pulse" aria-hidden="true" />
                {t('op.pos.scan.active')}
              </span>
            </div>
          </header>
          <label className="pos-search">
            <Search size={14} />
```

на:

```tsx
              <span className="ui-scanner-badge" aria-label={t('op.pos.scan.active')}>
                <span className="ui-scanner-pulse" aria-hidden="true" />
                {t('op.pos.scan.active')}
              </span>
            </div>
          </header>
          <label className="ui-search-field pos-catalog-search">
            <Search size={14} />
```

- [ ] **Step 5: Прогнать тесты POS — убедиться, что рендер не сломан**

Run: `cd src/AFK4.Operator.App.Web && bun test src/BackendPosWorkspace.test.tsx`
Expected: PASS (без изменений в наборе тестов).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/02-ui-kit.css src/AFK4.Operator.App.Web/src/styles/11-pos.css src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx
git commit -m "refactor(operator): вынести .ui-search-field/.ui-scanner-badge в общий ui-kit, мигрировать POS"
```

---

## Task 2: Компонент `StockHero`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/StockHero.tsx`
- Create: `src/AFK4.Operator.App.Web/src/stock/StockHero.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (добавить `.stock-hero*`)

**Interfaces:**
- Производит: `StockHero({ label: string; value: ReactNode; sub?: ReactNode; tone: 'neutral' | 'ok' | 'warning' | 'attention' | 'muted' })` — используется в Task 4–7.

- [ ] **Step 1: Написать падающий тест**

```tsx
// src/AFK4.Operator.App.Web/src/stock/StockHero.test.tsx
import { describe, it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { StockHero } from './StockHero';

describe('StockHero', () => {
  it('рендерит подпись, значение и опциональный саб-текст', () => {
    render(<StockHero label="Стоимость склада" value="120 с." sub="73 ед · по средней закупочной" tone="neutral" />);
    expect(screen.getByText('Стоимость склада')).toBeInTheDocument();
    expect(screen.getByText('120 с.')).toBeInTheDocument();
    expect(screen.getByText('73 ед · по средней закупочной')).toBeInTheDocument();
  });

  it('тон определяет модификатор класса', () => {
    const { container } = render(<StockHero label="Нужно дозаказать" value={1} tone="attention" />);
    expect(container.querySelector('.stock-hero')).toHaveClass('stock-hero--attention');
  });

  it('без sub — подпись-примечание не рендерится', () => {
    const { container } = render(<StockHero label="X" value="1" tone="muted" />);
    expect(container.querySelector('.stock-hero-sub')).toBeNull();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockHero.test.tsx`
Expected: FAIL с `Cannot find module './StockHero'` (файл ещё не создан).

- [ ] **Step 3: Реализовать компонент**

```tsx
// src/AFK4.Operator.App.Web/src/stock/StockHero.tsx
import type { ReactNode } from 'react';

export type StockHeroTone = 'neutral' | 'ok' | 'warning' | 'attention' | 'muted';

export function StockHero({
  label,
  value,
  sub,
  tone,
}: {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  tone: StockHeroTone;
}) {
  return (
    <div className={`stock-hero stock-hero--${tone}`}>
      <span className="stock-hero-label">{label}</span>
      <strong className="stock-hero-value">{value}</strong>
      {sub !== undefined && sub !== null && <span className="stock-hero-sub">{sub}</span>}
    </div>
  );
}
```

- [ ] **Step 4: Добавить CSS в `22-stock.css`**

Вставить после блока `.rowact` / перед `/* ── Сводка (правая панель) ── */` (текущая строка 259):

```css
/* ── Герой сводки: крупная метрика с цветным левым кантом (мирроринг seat-hero/booking-status-card) ── */
.stock-hero {
  display: flex;
  flex-direction: column;
  gap: 3px;
  padding: 10px 12px;
  border: 1px solid var(--border-default);
  border-left-width: 3px;
  border-radius: var(--radius-md);
  background: var(--surface-card);
}

.stock-hero-label {
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-size: 11px;
  font-weight: 700;
  color: var(--text-tertiary);
}

.stock-hero-value {
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: var(--text-xl);
  font-weight: 700;
  color: var(--text-primary);
  line-height: 1.1;
}

.stock-hero-sub {
  font-size: 11px;
  color: var(--text-quaternary);
}

.stock-hero--neutral { border-left-color: var(--accent); }
.stock-hero--ok { border-left-color: var(--accent); }
.stock-hero--ok .stock-hero-value { color: var(--accent-text); }
.stock-hero--warning { border-left-color: var(--warning); }
.stock-hero--warning .stock-hero-value { color: var(--warning-text); }
.stock-hero--attention { border-left-color: var(--danger); }
.stock-hero--attention .stock-hero-value { color: var(--danger-text); }
.stock-hero--muted { border-left-color: var(--border-default); }
.stock-hero--muted .stock-hero-value { color: var(--text-tertiary); }
```

- [ ] **Step 5: Запустить тест — убедиться, что проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockHero.test.tsx`
Expected: PASS (3 теста).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/StockHero.tsx src/AFK4.Operator.App.Web/src/stock/StockHero.test.tsx src/AFK4.Operator.App.Web/src/styles/22-stock.css
git commit -m "feat(operator-stock): компонент StockHero (герой-метрика с тоном)"
```

---

## Task 3: Компонент `ScanSearchBar`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/ScanSearchBar.tsx`
- Create: `src/AFK4.Operator.App.Web/src/stock/ScanSearchBar.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (добавить `.stock-scanbar*`)

**Interfaces:**
- Консьюмит: `.ui-search-field`, `.ui-scanner-badge`/`.ui-scanner-pulse` из Task 1.
- Производит: `ScanSearchBar({ icon: ReactNode; value: string; onChange: (value: string) => void; placeholder: string; ariaLabel: string; hint?: string; trailing?: ReactNode })` — используется в Task 5 (Приёмка) и Task 7 (Инвентаризация).

- [ ] **Step 1: Написать падающий тест**

```tsx
// src/AFK4.Operator.App.Web/src/stock/ScanSearchBar.test.tsx
import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { Search } from 'lucide-react';
import { ScanSearchBar } from './ScanSearchBar';

describe('ScanSearchBar', () => {
  it('рендерит поле поиска, бейдж «Сканер активен» и подсказку; зовёт onChange', () => {
    const onChange = mock((_value: string) => {});
    render(
      <I18nProvider initialLocale="ru">
        <ScanSearchBar
          icon={<Search size={16} aria-hidden="true" />}
          value=""
          onChange={onChange}
          placeholder="Название или SKU…"
          ariaLabel="Добавить товар"
          hint="Найдите товар по названию или SKU"
        />
      </I18nProvider>
    );
    expect(screen.getByLabelText('Сканер активен')).toBeInTheDocument();
    expect(screen.getByText('Найдите товар по названию или SKU')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Добавить товар'), { target: { value: 'cola' } });
    expect(onChange).toHaveBeenCalledWith('cola');
  });

  it('trailing-контент рендерится в полосе', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ScanSearchBar
          icon={<Search size={16} aria-hidden="true" />}
          value=""
          onChange={() => {}}
          placeholder="Поиск товара"
          ariaLabel="Поиск товара"
          trailing={<button type="button">Сброс</button>}
        />
      </I18nProvider>
    );
    expect(screen.getByRole('button', { name: 'Сброс' })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ScanSearchBar.test.tsx`
Expected: FAIL с `Cannot find module './ScanSearchBar'`.

- [ ] **Step 3: Реализовать компонент**

```tsx
// src/AFK4.Operator.App.Web/src/stock/ScanSearchBar.tsx
import type { ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';

export function ScanSearchBar({
  icon,
  value,
  onChange,
  placeholder,
  ariaLabel,
  hint,
  trailing,
}: {
  icon: ReactNode;
  value: string;
  onChange: (value: string) => void;
  placeholder: string;
  ariaLabel: string;
  hint?: string;
  trailing?: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <div className="stock-scanbar">
      <label className="ui-search-field stock-scanbar-search">
        {icon}
        <input
          type="search"
          aria-label={ariaLabel}
          placeholder={placeholder}
          value={value}
          onChange={(event) => onChange(event.currentTarget.value)}
        />
      </label>
      <span className="ui-scanner-badge" aria-label={t('op.pos.scan.active')}>
        <span className="ui-scanner-pulse" aria-hidden="true" />
        {t('op.pos.scan.active')}
      </span>
      {trailing}
      {hint && <p className="stock-scanbar-hint">{hint}</p>}
    </div>
  );
}
```

- [ ] **Step 4: Добавить CSS в `22-stock.css`**

Вставить сразу после блока `.stock-hero*` из Task 2:

```css
/* ── Строка сканера/поиска (общий паттерн Приёмки/Инвентаризации — чром из .ui-search-field) ── */
.stock-scanbar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  margin-bottom: 8px;
}

.stock-scanbar-search {
  flex: 1;
  min-width: 200px;
}

.stock-scanbar-search svg {
  flex-shrink: 0;
  color: var(--text-tertiary);
}

.stock-scanbar-hint {
  flex-basis: 100%;
  margin: 0;
  color: var(--text-tertiary);
  font-size: 11px;
}
```

- [ ] **Step 5: Запустить тест — убедиться, что проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ScanSearchBar.test.tsx`
Expected: PASS (2 теста).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ScanSearchBar.tsx src/AFK4.Operator.App.Web/src/stock/ScanSearchBar.test.tsx src/AFK4.Operator.App.Web/src/styles/22-stock.css
git commit -m "feat(operator-stock): компонент ScanSearchBar (единая полоса поиска+сканера)"
```

---

## Task 4: Остатки — два героя вместо плоских блоков

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx:264-305`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.test.tsx:36-42`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (новые ключи `reorderTitle`/`reorderSub`, удалить `lowCount`/`outCount`)

**Interfaces:**
- Консьюмит: `StockHero` (Task 2), `summarize()`/`stockStatus()` из `./stockLevels` (без изменений).

- [ ] **Step 1: Обновить i18n-ключи**

В `locales/ru.json` заменить (строки 2410-2411):
```json
  "op.stock.summary.lowCount": "Мало товаров",
  "op.stock.summary.outCount": "Закончились",
```
на:
```json
  "op.stock.summary.reorderTitle": "Нужно дозаказать",
  "op.stock.summary.reorderSub": "мало: {low} · нет: {out}",
```

В `locales/en.json` заменить (строки 2212-2213):
```json
  "op.stock.summary.lowCount": "Products low",
  "op.stock.summary.outCount": "Out of stock",
```
на:
```json
  "op.stock.summary.reorderTitle": "Needs reordering",
  "op.stock.summary.reorderSub": "low: {low} · out: {out}",
```

В `locales/tg.json` заменить (строки 2212-2213):
```json
  "op.stock.summary.lowCount": "Молҳо кам",
  "op.stock.summary.outCount": "Тамом шуд",
```
на:
```json
  "op.stock.summary.reorderTitle": "Ба фармоиш ниёз дорад",
  "op.stock.summary.reorderSub": "кам: {low} · тамом: {out}",
```

Run: `cd packages/i18n && bun run gen`
Expected: `generated .../packages/i18n/src/messages.ts from 3 locales` (без ошибок).

- [ ] **Step 2: Обновить падающий тест — новая структура сводки**

Заменить в `StockLevelsWorkspace.test.tsx` (строки 36-42):

```tsx
  it('стат-карточка «Стоимость склада» самодостаточна: счётчики low/out — соседи, не вложены', async () => {
    const { container } = view();
    await screen.findByText('Cola 0.5');
    const statCards = container.querySelectorAll('.ui-card--stat');
    expect(statCards).toHaveLength(1);
    expect(statCards[0].querySelector('.mv')).toBeNull();
  });
```

на:

```tsx
  it('два героя сводки: «Стоимость склада» нейтральный, «Нужно дозаказать» тонирован по худшему статусу', async () => {
    const { container } = view();
    await screen.findByText('Cola 0.5');
    const heroes = container.querySelectorAll('.stock-hero');
    expect(heroes).toHaveLength(2);
    expect(heroes[0]).toHaveClass('stock-hero--neutral');
    expect(within(heroes[0] as HTMLElement).getByText('Стоимость склада')).toBeInTheDocument();
    // Red Bull (low, 8/10) + Вода (out, 0) → худший статус out → tone attention, счётчик = 2
    expect(heroes[1]).toHaveClass('stock-hero--attention');
    expect(within(heroes[1] as HTMLElement).getByText('2')).toBeInTheDocument();
  });
```

- [ ] **Step 3: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: FAIL — `heroes` пуст (JSX ещё не переписан).

- [ ] **Step 4: Переписать `<aside>` в `StockLevelsWorkspace.tsx`**

Добавить импорт (рядом с другими импортами из `./`):

```tsx
import { StockHero } from './StockHero';
```

Заменить блок (текущие строки 266-305):

```tsx
      {/* ── Сводка ── */}
      <aside className="stock-summary">
        <div className="ui-card ui-card--stat">
          <span>{t('op.stock.summary.totalValue')}</span>
          <strong><Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} /></strong>
        </div>
        <div className="ctx-sub">
          {t('op.stock.summary.totalSub', { count: items.reduce((acc, i) => acc + Math.max(i.stockOnHand, 0), 0) })}
        </div>
        <div className="mv">
          <span>{t('op.stock.summary.lowCount')}</span>
          <b className="warning-text">{summary.lowCount}</b>
        </div>
        <div className="mv">
          <span>{t('op.stock.summary.outCount')}</span>
          <b className={summary.outCount > 0 ? 'danger-text' : undefined}>{summary.outCount}</b>
        </div>

        {orderItems.length > 0 && (
          <div className="ctx-card">
            <h3 className="ctx-title">
              {t('op.stock.summary.orderTitle')}
              {' '}
              <span className="warning-text">{orderItems.length}</span>
            </h3>
            {orderItems.map((item) => {
              const s = stockStatus(item);
              return (
                <div key={item.productId} className="order-item" title={item.name}>
                  <strong className="order-item-name">{item.name}</strong>
                  <span className={`oq ${s}`}>{item.stockOnHand}/{item.reorderThreshold}</span>
                </div>
              );
            })}
            <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={!onReceive} onClick={() => onReceive?.()}>
              {t('op.stock.summary.orderBtn')}
            </button>
          </div>
        )}
      </aside>
```

на:

```tsx
      {/* ── Сводка: два героя + список к заказу ── */}
      <aside className="stock-summary">
        <StockHero
          label={t('op.stock.summary.totalValue')}
          value={<Money minorUnits={summary.totalValueMinorUnits} currencyCode={currencyCode} />}
          sub={t('op.stock.summary.totalSub', { count: items.reduce((acc, i) => acc + Math.max(i.stockOnHand, 0), 0) })}
          tone="neutral"
        />

        <StockHero
          label={t('op.stock.summary.reorderTitle')}
          value={orderItems.length}
          sub={t('op.stock.summary.reorderSub', { low: summary.lowCount, out: summary.outCount })}
          tone={summary.outCount > 0 ? 'attention' : summary.lowCount > 0 ? 'warning' : 'muted'}
        />

        {orderItems.length > 0 && (
          <section className="stock-section">
            <h3 className="ctx-title">{t('op.stock.summary.orderTitle')}</h3>
            {orderItems.map((item) => {
              const s = stockStatus(item);
              return (
                <div key={item.productId} className="order-item" title={item.name}>
                  <strong className="order-item-name">{item.name}</strong>
                  <span className={`oq ${s}`}>{item.stockOnHand}/{item.reorderThreshold}</span>
                </div>
              );
            })}
            <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={!onReceive} onClick={() => onReceive?.()}>
              {t('op.stock.summary.orderBtn')}
            </button>
          </section>
        )}
      </aside>
```

Примечание: `op.stock.summary.orderTitle` («Заказать») теперь используется только как заголовок списка-секции (счётчик из него убран — счётчик живёт в героях выше), текст ключа не меняется.

- [ ] **Step 5: Запустить тесты файла — убедиться, что проходят**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: PASS (5 тестов).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.tsx src/AFK4.Operator.App.Web/src/stock/StockLevelsWorkspace.test.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "refactor(operator-stock): Остатки — два героя сводки вместо плоских счётчиков"
```

---

## Task 5: Приёмка — герой «Сумма» + `ScanSearchBar`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (удалить `op.stock.receiving.totalTitle`)

**Interfaces:**
- Консьюмит: `StockHero` (Task 2), `ScanSearchBar` (Task 3).

- [ ] **Step 1: Удалить орфан-ключ `op.stock.receiving.totalTitle`**

Удалить строку из `locales/ru.json` (2343): `"op.stock.receiving.totalTitle": "Итог приёмки",`
Удалить строку из `locales/en.json` (2145): `"op.stock.receiving.totalTitle": "Receipt total",`
Удалить строку из `locales/tg.json` (2145): `"op.stock.receiving.totalTitle": "Ҷамъи қабул",`

Run: `cd packages/i18n && bun run gen`

- [ ] **Step 2: Прогнать тесты Приёмки — зафиксировать текущее зелёное состояние перед рефакторингом**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ReceivingWorkspace.test.tsx`
Expected: PASS (все 11 существующих тестов — рефакторинг не должен их сломать при верной реализации; после Step 3-4 прогоняем снова).

- [ ] **Step 3: Обновить импорты**

Заменить (строка 1-3):

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, Minus, Plus, ScanLine, X } from 'lucide-react';
```

на:

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, Minus, Plus, Search, X } from 'lucide-react';
```

Добавить рядом с остальными `./`-импортами:

```tsx
import { StockHero } from './StockHero';
import { ScanSearchBar } from './ScanSearchBar';
```

- [ ] **Step 4: Заменить полосу добавления товара**

Заменить блок (текущие строки 171-187):

```tsx
        <div className="recv-add">
          <div className="recv-add-ico"><Boxes size={20} aria-hidden="true" /></div>
          <div className="ui-field recv-add-field">
            <input
              type="search"
              aria-label={t('op.stock.receiving.addLabel')}
              placeholder={t('op.stock.receiving.search')}
              value={search}
              onChange={(event) => setSearch(event.currentTarget.value)}
            />
            <span className="recv-add-hint">{t('op.stock.receiving.addHint')}</span>
          </div>
          <span className="ui-chip ui-chip--status is-live" aria-label={t('op.pos.scan.active')}>
            <ScanLine size={14} aria-hidden="true" />
            {t('op.pos.scan.active')}
          </span>
        </div>
```

на:

```tsx
        <ScanSearchBar
          icon={<Search size={16} aria-hidden="true" />}
          value={search}
          onChange={setSearch}
          placeholder={t('op.stock.receiving.search')}
          ariaLabel={t('op.stock.receiving.addLabel')}
          hint={t('op.stock.receiving.addHint')}
        />
```

- [ ] **Step 5: Заменить `<aside>`**

Заменить блок (текущие строки 263-292):

```tsx
      {/* ── Накладная (правая колонка) ── */}
      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.receiving.invoiceTitle')}</h3>
          <label className="ui-field">
            <span>{t('op.stock.receiving.supplier')}</span>
            <input value={supplier} disabled={posting} onChange={(event) => setSupplier(event.currentTarget.value)} />
          </label>
          <label className="ui-field">
            <span>{t('op.stock.receiving.invoiceNo')}</span>
            <input value={invoiceNo} disabled={posting} placeholder={t('op.stock.receiving.invoiceNoHint')} onChange={(event) => setInvoiceNo(event.currentTarget.value)} />
          </label>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.receiving.totalTitle')}</h3>
          <div className="ui-card ui-card--stat">
            <span>{t('op.stock.receiving.totalSum')}</span>
            <strong><Money minorUnits={totals.sumMinorUnits} currencyCode={currencyCode} /></strong>
          </div>
          <div className="mv"><span>{t('op.stock.receiving.totalPositions')}</span><b>{totals.positions}</b></div>
          <div className="mv"><span>{t('op.stock.receiving.totalUnits')}</span><b>{totals.units}</b></div>
          <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={lines.length === 0 || posting} onClick={postReceipt}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.receiving.posting') : t('op.stock.receiving.post')}
          </button>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.receiving.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </div>
      </aside>
```

на:

```tsx
      {/* ── Накладная (правая колонка) ── */}
      <aside className="stock-summary">
        <section className="stock-section">
          <h3 className="ctx-title">{t('op.stock.receiving.invoiceTitle')}</h3>
          <label className="ui-field">
            <span>{t('op.stock.receiving.supplier')}</span>
            <input value={supplier} disabled={posting} onChange={(event) => setSupplier(event.currentTarget.value)} />
          </label>
          <label className="ui-field">
            <span>{t('op.stock.receiving.invoiceNo')}</span>
            <input value={invoiceNo} disabled={posting} placeholder={t('op.stock.receiving.invoiceNoHint')} onChange={(event) => setInvoiceNo(event.currentTarget.value)} />
          </label>
        </section>

        <StockHero
          label={t('op.stock.receiving.totalSum')}
          value={<Money minorUnits={totals.sumMinorUnits} currencyCode={currencyCode} />}
          tone={lines.length > 0 ? 'neutral' : 'muted'}
        />

        <section className="stock-section">
          <div className="mv"><span>{t('op.stock.receiving.totalPositions')}</span><b>{totals.positions}</b></div>
          <div className="mv"><span>{t('op.stock.receiving.totalUnits')}</span><b>{totals.units}</b></div>
          <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={lines.length === 0 || posting} onClick={postReceipt}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.receiving.posting') : t('op.stock.receiving.post')}
          </button>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.receiving.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </section>
      </aside>
```

- [ ] **Step 6: Прогнать тесты — убедиться, что всё зелёное**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/ReceivingWorkspace.test.tsx`
Expected: PASS (11 тестов — `aria-label`'ы «Добавить товар»/«Сканер активен» сохранены через `ScanSearchBar`, поведение поиска/сканера не менялось).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "refactor(operator-stock): Приёмка — герой «Сумма» + единая ScanSearchBar"
```

---

## Task 6: Журнал — герой «Чистое движение»

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.tsx:208-233`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (удалить `op.stock.journal.summary.title`)

**Interfaces:**
- Консьюмит: `StockHero` (Task 2).

- [ ] **Step 1: Удалить орфан-ключ `op.stock.journal.summary.title`**

Удалить строку из `locales/ru.json` (2370): `"op.stock.journal.summary.title": "Итог за период",`
Удалить строку из `locales/en.json` (2172): `"op.stock.journal.summary.title": "Period summary",`
Удалить строку из `locales/tg.json` (2172): `"op.stock.journal.summary.title": "Хулосаи давра",`

Run: `cd packages/i18n && bun run gen`

- [ ] **Step 2: Прогнать тесты Журнала — зафиксировать зелёную базу**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/JournalWorkspace.test.tsx`
Expected: PASS (4 теста).

- [ ] **Step 3: Добавить импорт**

Добавить рядом с остальными `./`-импортами в `JournalWorkspace.tsx`:

```tsx
import { StockHero } from './StockHero';
```

- [ ] **Step 4: Заменить `<aside>`**

Заменить блок (текущие строки 208-233):

```tsx
      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.journal.period.title')}</h3>
          <div className="period">
            {PERIODS.map((value) => (
              <button
                key={value}
                type="button"
                className={`ui-chip ui-chip--filter${period === value ? ' is-active' : ''}`}
                aria-pressed={period === value}
                onClick={() => setPeriod(value)}
              >
                {t(PERIOD_LABEL_KEYS[value])}
              </button>
            ))}
          </div>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.journal.summary.title')}</h3>
          <div className="totrow"><span>{t('op.stock.journal.summary.inbound')}</span><b className="in">+{summary.inboundQty} · <Money minorUnits={summary.inboundSumMinor} currencyCode={currencyCode} /></b></div>
          <div className="totrow"><span>{t('op.stock.journal.summary.sold')}</span><b>−{summary.soldQty}</b></div>
          <div className="totrow"><span>{t('op.stock.journal.summary.writtenOff')}</span><b className="wn">−{summary.writtenOffQty} · <Money minorUnits={summary.writtenOffSumMinor} currencyCode={currencyCode} /></b></div>
          <div className="totrow net"><span>{t('op.stock.journal.summary.net')}</span><b>{summary.netQty > 0 ? '+' : ''}{summary.netQty}</b></div>
        </div>
      </aside>
```

на:

```tsx
      <aside className="stock-summary">
        <section className="stock-section">
          <h3 className="ctx-title">{t('op.stock.journal.period.title')}</h3>
          <div className="period">
            {PERIODS.map((value) => (
              <button
                key={value}
                type="button"
                className={`ui-chip ui-chip--filter${period === value ? ' is-active' : ''}`}
                aria-pressed={period === value}
                onClick={() => setPeriod(value)}
              >
                {t(PERIOD_LABEL_KEYS[value])}
              </button>
            ))}
          </div>
        </section>

        <StockHero
          label={t('op.stock.journal.summary.net')}
          value={summary.netQty > 0 ? `+${summary.netQty}` : String(summary.netQty)}
          tone={summary.netQty > 0 ? 'ok' : summary.netQty < 0 ? 'warning' : 'muted'}
        />

        <section className="stock-section">
          <div className="mv"><span>{t('op.stock.journal.summary.inbound')}</span><b className="in">+{summary.inboundQty} · <Money minorUnits={summary.inboundSumMinor} currencyCode={currencyCode} /></b></div>
          <div className="mv"><span>{t('op.stock.journal.summary.sold')}</span><b>−{summary.soldQty}</b></div>
          <div className="mv"><span>{t('op.stock.journal.summary.writtenOff')}</span><b className="wn">−{summary.writtenOffQty} · <Money minorUnits={summary.writtenOffSumMinor} currencyCode={currencyCode} /></b></div>
        </section>
      </aside>
```

- [ ] **Step 5: Прогнать тесты — убедиться, что проходят**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/JournalWorkspace.test.tsx`
Expected: PASS (4 теста — ни один не проверял `.totrow`/`ctx-title` дословно, риска регрессии нет).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "refactor(operator-stock): Журнал — герой «Чистое движение», плоские строки вместо totrow"
```

---

## Task 7: Инвентаризация — герой «Итог по себест.» + `ScanSearchBar`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (удалить `op.stock.inventory.totalTitle`)

**Interfaces:**
- Консьюмит: `StockHero` (Task 2), `ScanSearchBar` (Task 3).

- [ ] **Step 1: Удалить орфан-ключ `op.stock.inventory.totalTitle`**

Удалить строку из `locales/ru.json` (2316): `"op.stock.inventory.totalTitle": "Итог инвентаризации",`
Удалить строку из `locales/en.json` (2118): `"op.stock.inventory.totalTitle": "Inventory summary",`
Удалить строку из `locales/tg.json` (2118): `"op.stock.inventory.totalTitle": "Натиҷаи барӯйхатгирӣ",`

Run: `cd packages/i18n && bun run gen`

- [ ] **Step 2: Прогнать тесты Инвентаризации — зафиксировать зелёную базу**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/InventoryWorkspace.test.tsx`
Expected: PASS (9 тестов).

- [ ] **Step 3: Обновить импорты**

Заменить (строка 1-3):

```tsx
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, RotateCcw, ScanLine } from 'lucide-react';
```

на:

```tsx
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Boxes, Check, RotateCcw, Search } from 'lucide-react';
```

Добавить рядом с остальными `./`-импортами:

```tsx
import { StockHero } from './StockHero';
import { ScanSearchBar } from './ScanSearchBar';
```

- [ ] **Step 4: Заменить строку сканера**

Заменить блок (текущие строки 156-175):

```tsx
        <div className="inv-scanbar">
          <span className="inv-scanbar-ico"><ScanLine size={18} aria-hidden="true" /></span>
          <span className="inv-scanbar-lbl" aria-label={t('op.pos.scan.active')}>
            {t('op.stock.inventory.scanHint')}<i className="inv-caret" aria-hidden="true" />
          </span>
          <input
            className="inv-search"
            type="search"
            aria-label={t('op.stock.inventory.search')}
            placeholder={t('op.stock.inventory.search')}
            value={search}
            onChange={(event) => setSearch(event.currentTarget.value)}
          />
          {hasCounts && (
            <button type="button" className="ui-btn ui-btn--sm ui-btn--ghost" disabled={posting} onClick={() => setLines((c) => resetCounts(c))}>
              <RotateCcw size={13} aria-hidden="true" />
              {t('op.stock.inventory.reset')}
            </button>
          )}
        </div>
```

на:

```tsx
        <ScanSearchBar
          icon={<Search size={16} aria-hidden="true" />}
          value={search}
          onChange={setSearch}
          placeholder={t('op.stock.inventory.search')}
          ariaLabel={t('op.stock.inventory.search')}
          hint={t('op.stock.inventory.scanHint')}
          trailing={hasCounts ? (
            <button type="button" className="ui-btn ui-btn--sm ui-btn--ghost" disabled={posting} onClick={() => setLines((c) => resetCounts(c))}>
              <RotateCcw size={13} aria-hidden="true" />
              {t('op.stock.inventory.reset')}
            </button>
          ) : undefined}
        />
```

- [ ] **Step 5: Заменить `<aside>`**

Заменить блок (текущие строки 236-257):

```tsx
      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.inventory.progressTitle')}</h3>
          <div className="inv-prog"><i style={{ width: `${pct}%` }} /></div>
          <div className="inv-progtxt"><span>{t('op.stock.inventory.counted')}</span><b>{totals.countedCount} / {totals.trackedCount}</b></div>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.inventory.totalTitle')}</h3>
          <div className="mv"><span>{t('op.stock.inventory.discrepancies')}</span><b>{totals.discrepancies}</b></div>
          <div className="mv"><span>{t('op.stock.inventory.shortage')}</span><b className="warning-text">-{totals.shortageUnits} {unit} · <Money minorUnits={totals.shortageSumMinorUnits} currencyCode={currencyCode} /></b></div>
          <div className="mv"><span>{t('op.stock.inventory.surplus')}</span><b className="inv-pos">+{totals.surplusUnits} {unit} · <Money minorUnits={totals.surplusSumMinorUnits} currencyCode={currencyCode} /></b></div>
          <div className="mv recv-grand"><span>{t('op.stock.inventory.netCost')}</span><b className={totals.netSumMinorUnits < 0 ? 'warning-text' : totals.netSumMinorUnits > 0 ? 'inv-pos' : undefined}><Money minorUnits={totals.netSumMinorUnits} currencyCode={currencyCode} signed={totals.netSumMinorUnits !== 0} /></b></div>
          <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={adjustments.length === 0 || posting} onClick={postInventory}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.inventory.posting') : t('op.stock.inventory.post')}
          </button>
          <p className="ctx-note">{t('op.stock.inventory.willCreate', { count: adjustments.length })}</p>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.inventory.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </div>
      </aside>
```

на:

```tsx
      <aside className="stock-summary">
        <section className="stock-section">
          <h3 className="ctx-title">{t('op.stock.inventory.progressTitle')}</h3>
          <div className="inv-prog"><i style={{ width: `${pct}%` }} /></div>
          <div className="inv-progtxt"><span>{t('op.stock.inventory.counted')}</span><b>{totals.countedCount} / {totals.trackedCount}</b></div>
        </section>

        <StockHero
          label={t('op.stock.inventory.netCost')}
          value={<Money minorUnits={totals.netSumMinorUnits} currencyCode={currencyCode} signed={totals.netSumMinorUnits !== 0} />}
          tone={totals.netSumMinorUnits < 0 ? 'attention' : totals.netSumMinorUnits > 0 ? 'ok' : 'muted'}
        />

        <section className="stock-section">
          <div className="mv"><span>{t('op.stock.inventory.discrepancies')}</span><b>{totals.discrepancies}</b></div>
          <div className="mv"><span>{t('op.stock.inventory.shortage')}</span><b className="warning-text">-{totals.shortageUnits} {unit} · <Money minorUnits={totals.shortageSumMinorUnits} currencyCode={currencyCode} /></b></div>
          <div className="mv"><span>{t('op.stock.inventory.surplus')}</span><b className="inv-pos">+{totals.surplusUnits} {unit} · <Money minorUnits={totals.surplusSumMinorUnits} currencyCode={currencyCode} /></b></div>
          <button type="button" className="ui-btn ui-btn--primary ui-btn--block" disabled={adjustments.length === 0 || posting} onClick={postInventory}>
            <Check size={16} aria-hidden="true" />
            {posting ? t('op.stock.inventory.posting') : t('op.stock.inventory.post')}
          </button>
          <p className="ctx-note">{t('op.stock.inventory.willCreate', { count: adjustments.length })}</p>
          {post.kind === 'done' && <p className="recv-status ok">{t('op.stock.inventory.posted', { count: post.count })}</p>}
          {post.kind === 'error' && <p className="recv-status err" role="alert">{post.detail}</p>}
        </section>
      </aside>
```

- [ ] **Step 6: Прогнать тесты — убедиться, что проходят**

Run: `cd src/AFK4.Operator.App.Web && bun test src/stock/InventoryWorkspace.test.tsx`
Expected: PASS (9 тестов).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/InventoryWorkspace.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "refactor(operator-stock): Инвентаризация — герой «Итог по себест.» + единая ScanSearchBar"
```

---

## Task 8: Уборка мёртвого CSS + финальный гейт

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (удалить осиротевшие правила)

**Interfaces:** нет новых — только удаление.

- [ ] **Step 1: Убедиться, что классы действительно осиротели**

Run:
```bash
cd src/AFK4.Operator.App.Web
grep -rn "ctx-card\|ctx-sub\|recv-add\|inv-scanbar\|inv-search\|recv-grand\|\.totrow" src/**/*.tsx
```
Expected: пусто (ни один `.tsx`-файл больше не ссылается на эти классы) — если что-то найдётся, разобраться перед удалением CSS.

- [ ] **Step 2: Удалить мёртвые правила из `22-stock.css`**

Удалить блок `.ctx-card { ... }` (был на строке 268-273).

Удалить блок `.ctx-sub { ... }` (был на строке 288-292) — подпись героя теперь через `.stock-hero-sub` (Task 2).

Удалить блок «Полоса добавления товара» целиком — `.recv-add`, `.recv-add-ico`, `.recv-add-field`, `.recv-add-hint` (были на строках 362-387).

Удалить `.recv-grand`, `.recv-grand b` (были на строках 542-543).

Удалить `.inv-scanbar`, `.inv-scanbar-ico`, `.inv-scanbar-lbl`, `.inv-caret`, `@keyframes inv-blink`, `.inv-search` (были на строках 658-668).

Удалить `.inv-search:focus-visible` (была на строке 721).

Удалить `.stock-summary .totrow`, `.stock-summary .totrow:last-child`, `.stock-summary .totrow b`, `.stock-summary .totrow b.in`, `.stock-summary .totrow b.wn`, `.stock-summary .totrow.net b` (были на строках 611-624).

- [ ] **Step 3: Обновить `.stock-summary` — единая приподнятая панель + добавить `.stock-section`**

Заменить (текущие строки 260-265):

```css
/* ── Сводка (правая панель) ── */
.stock-summary {
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow: auto;
}
```

на:

```css
/* ── Сводка (правая панель): единая приподнятая панель, hairline-секции внутри ── */
.stock-summary {
  display: flex;
  flex-direction: column;
  gap: 12px;
  border: 1px solid var(--border-soft);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  background: var(--surface-elevated);
  box-shadow: var(--shadow-card);
  overflow: auto;
}

.stock-section {
  padding-top: 14px;
  border-top: 1px solid var(--border-soft);
}

.stock-section:first-child {
  padding-top: 0;
  border-top: 0;
}
```

- [ ] **Step 4: Прогнать весь набор тестов Операторского фронта**

Run: `cd src/AFK4.Operator.App.Web && bun test`
Expected: PASS, все существующие + новые тесты (StockHero, ScanSearchBar, 4 stock-workspace, BackendPosWorkspace, StockWorkspace, WriteOffDialog и весь остальной набор) зелёные.

- [ ] **Step 5: Прогнать сборку (тайпчек тестов и сужений — обязательный гейт проекта)**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешная сборка без ошибок TypeScript.

- [ ] **Step 6: Прогнать i18n-пакет (guard-тесты tg≠ru, voice-глоссарий)**

Run: `cd packages/i18n && bun test`
Expected: PASS — новые ключи `reorderTitle`/`reorderSub` проходят tg-honesty guard (tg-текст не совпадает с ru).

- [ ] **Step 7: Ручная проверка через WPF-превью**

Открыть превью оператора (см. навык `operator-wpf-preview` или `bun run dev` на порту 5174/5175), пройти по всем 4 вкладкам Склада:
- Остатки — два героя видны, тон второго меняется корректно (создать/убрать «на исходе»/«нет в наличии» товар через Приёмку/Списание и проверить перекраску).
- Приёмка — герой «Сумма», строка поиска с бейджем «Сканер активен» идентична визуально POS; скан штрихкода (физическая клавиатура — эмуляция HID) добавляет строку.
- Журнал — герой «Чистое движение» меняет тон по знаку.
- Инвентаризация — герой «Итог по себест.», строка сканера идентична Приёмке; скан фокусирует нужную строку.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/22-stock.css
git commit -m "refactor(operator-stock): удалить осиротевший CSS после редизайна сайдбара/сканера"
```

---

## Self-Review Notes

- **Spec coverage:** Часть 1 спеки (4 вкладки → герои) — Tasks 4-7. Часть 2 спеки (стандартизация сканера) — Tasks 1, 3, 5, 7. CSS-уборка и токены — Task 8. Тестирование — во все таски встроены прогоны, финальный build/i18n-гейт — Task 8.
- **Type consistency:** `StockHeroTone` — единый union `'neutral'|'ok'|'warning'|'attention'|'muted'` используется одинаково в Tasks 4/5/6/7. `ScanSearchBar` props (`icon`/`value`/`onChange`/`placeholder`/`ariaLabel`/`hint`/`trailing`) — одинаковая сигнатура в Tasks 5 и 7.
- **Порядок задач важен:** Task 1 (общие атомы) → Task 2/3 (компоненты) → Task 4-7 (рефакторинг экранов, каждый независимо коммитится и тестируется) → Task 8 (уборка мёртвого CSS — безопасна только после того, как все 4 экрана перестали ссылаться на старые классы).
