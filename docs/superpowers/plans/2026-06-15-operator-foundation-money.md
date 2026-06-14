# Operator Этап 0 — «Деньги»: единый форматтер + знак валюты (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить технический ISO-код валюты («12 TJS») на человеческий локализованный знак («12 с.») во всём Operator UI, починив это в одной точке форматтера и вычистив inline-обходы.

**Architecture:** Знак валюты — единый источник `currencySymbol()` в пакете `@afk4/money`. Строковый форматтер денег выносится из раздутого `operatorHelpers.ts` в новый листовой модуль `currencyFormat.ts` (его импортируют и `operatorHelpers`, и `floorMapState` — это убирает риск циклического импорта и держит модуль с одной ответственностью). Десятичные/числовая локаль не меняются — правим только валютный суффикс и убираем хардкод `0 ${currencyCode}`.

**Tech Stack:** TypeScript, React 19, vanilla CSS. Тесты — `bun test` (happy-dom + jest-dom). Пакеты-воркспейсы `@afk4/money`, `@afk4/formatting`. Бандлер Vite. Bun: `~/.bun/bin/bun`.

---

## Реализуемая часть спека

Спек: `docs/superpowers/specs/2026-06-14-operator-foundation-design.md`, §7 «Деньги и валюта» + §10 (критерий «ни одного ISO-кода в money-surfaces UI; на плитке постоплата вида 54 с.»).

**Решение по реализации (отступление от буквы §7):** знак валюты живёт как const-мапа в `@afk4/money`, НЕ в i18n-json. Причина: §7 требует «фикс в одной точке»; i18n-вариант потребовал бы протащить `t()` в десятки вызовов `formatMinorUnits` (обратное «одной точке»). Следствие: знак не зависит от языка интерфейса (TJS → «с.» и в ru/tg, и в en). Для операторского приложения таджикского клуба это приемлемо. Если позже понадобится язык-зависимость — мапу легко перенести в i18n.

## Затрагиваемые файлы

- **Создать** `packages/money/src/index.ts` (дополнить) — `currencySymbol()` + мапа знаков. Единый источник.
- **Изменить** `packages/money/src/index.test.ts` — тесты `currencySymbol`.
- **Создать** `src/AFK4.Operator.App.Web/src/currencyFormat.ts` — `formatMinorUnits()` (со знаком). Листовой модуль.
- **Создать** `src/AFK4.Operator.App.Web/src/currencyFormat.test.ts` — юнит-тесты форматтера.
- **Изменить** `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` — убрать локальную `formatMinorUnits`, реэкспортировать из `currencyFormat`; `formatMoney` остаётся; вычистить фикстуру `fixturePlayers`.
- **Изменить** `src/AFK4.Operator.App.Web/src/floorMapState.ts` — `accruedCostText` через единый форматтер.
- **Изменить** `src/AFK4.Operator.App.Web/src/floorMapState.test.ts` — обновить ожидание.
- **Изменить** `src/AFK4.Operator.App.Web/src/App.test.tsx` — обновить 3 рендер-ассерта на знак.
- **Изменить** `BackendPaymentsWorkspace.tsx`, `BackendPosWorkspace.tsx`, `BackendPlayersWorkspace.tsx` — вычистить inline `` `0 ${currencyCode}` `` фолбэки.

---

## Task 1: Знак валюты как единый источник в `@afk4/money`

**Files:**
- Modify: `packages/money/src/index.ts`
- Test: `packages/money/src/index.test.ts`

- [ ] **Step 1: Написать падающий тест**

В конец `packages/money/src/index.test.ts` добавить:

```ts
import { currencySymbol } from './index';

it('maps known currency codes to short UI signs', () => {
  expect(currencySymbol('TJS')).toBe('с.');
  expect(currencySymbol('usd')).toBe('$');
});

it('falls back to the ISO code for unknown currencies', () => {
  expect(currencySymbol('GBP')).toBe('GBP');
});
```

(`import` добавить рядом с существующим `import { minorToMajor, majorToMinor } from './index';` — можно дописать `currencySymbol` в тот же список вместо отдельной строки.)

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `~/.bun/bin/bun test packages/money/src/index.test.ts`
Expected: FAIL — `currencySymbol is not a function` / export not found.

- [ ] **Step 3: Реализовать**

В конец `packages/money/src/index.ts` добавить:

```ts
/** Short, human-facing currency signs shown in the UI instead of raw ISO codes
 * (which read as technical jargon). Falls back to the ISO code for currencies
 * not listed here. */
export const currencySymbols: Record<string, string> = {
  TJS: 'с.',
  USD: '$',
  EUR: '€',
  RUB: '₽'
};

export function currencySymbol(currencyCode: string): string {
  return currencySymbols[currencyCode.toUpperCase()] ?? currencyCode;
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `~/.bun/bin/bun test packages/money/src/index.test.ts`
Expected: PASS (все it зелёные).

- [ ] **Step 5: Commit**

```bash
git add packages/money/src/index.ts packages/money/src/index.test.ts
git commit -m "feat(money): add currencySymbol() for human-facing currency signs"
```

---

## Task 2: Вынести форматтер в `currencyFormat.ts` и подключить знак

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/currencyFormat.ts`
- Create: `src/AFK4.Operator.App.Web/src/currencyFormat.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts:835-843` (удалить локальную `formatMinorUnits`), импорт-строки сверху
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx:203,1217,1309`

- [ ] **Step 1: Написать падающий тест форматтера**

Создать `src/AFK4.Operator.App.Web/src/currencyFormat.test.ts`:

```ts
import { it, expect } from 'bun:test';
import { formatMinorUnits } from './currencyFormat';
import { formatMoney } from './operatorHelpers';

it('formats minor units with the localized currency sign, not the ISO code', () => {
  expect(formatMinorUnits(1200, 'TJS')).toBe('12 с.');
  expect(formatMinorUnits(5400, 'TJS')).toBe('54 с.');
  expect(formatMinorUnits(2250, 'TJS')).toBe('22,5 с.');
});

it('uses the sign for other currencies and falls back to the code when unknown', () => {
  expect(formatMinorUnits(1000, 'USD')).toBe('10 $');
  expect(formatMinorUnits(1000, 'GBP')).toBe('10 GBP');
});

it('formatMoney treats null/missing as zero in the fallback currency', () => {
  expect(formatMoney(null, 'TJS')).toBe('0 с.');
  expect(formatMoney({ currencyCode: 'TJS', minorUnits: 1200 }, 'USD')).toBe('12 с.');
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `~/.bun/bin/bun test src/AFK4.Operator.App.Web/src/currencyFormat.test.ts`
Expected: FAIL — модуль `./currencyFormat` не найден.

- [ ] **Step 3: Создать листовой модуль форматтера**

Создать `src/AFK4.Operator.App.Web/src/currencyFormat.ts`:

```ts
import { minorToMajor, currencySymbol } from '@afk4/money';
import { formatNumber as formatLocaleNumber } from '@afk4/formatting';

/** Formats integer minor units as a human-facing money string with the localized
 * currency sign (e.g. 1200, 'TJS' -> '12 с.'). Whole amounts drop the fraction;
 * non-whole amounts keep up to 2 digits. Single source for all Operator money text. */
export function formatMinorUnits(minorUnits: number, currencyCode: string): string {
  const majorUnits = minorToMajor(minorUnits);
  const formatted = formatLocaleNumber(majorUnits, 'ru-RU', {
    maximumFractionDigits: Number.isInteger(majorUnits) ? 0 : 2,
    minimumFractionDigits: 0
  });

  return `${formatted} ${currencySymbol(currencyCode)}`;
}
```

- [ ] **Step 4: Заменить локальную `formatMinorUnits` в `operatorHelpers.ts` на реэкспорт**

В `operatorHelpers.ts` удалить старое определение (строки 835-843):

```ts
export function formatMinorUnits(minorUnits: number, currencyCode: string): string {
  const majorUnits = minorToMajor(minorUnits);
  const formatted = formatLocaleNumber(majorUnits, 'ru-RU', {
    maximumFractionDigits: Number.isInteger(majorUnits) ? 0 : 2,
    minimumFractionDigits: 0
  });

  return `${formatted} ${currencyCode}`;
}
```

и вместо него поставить реэкспорт + локальный импорт (чтобы `formatMoney` ниже продолжал видеть `formatMinorUnits`, и чтобы внешние `import { formatMinorUnits } from './operatorHelpers'` не сломались):

```ts
import { formatMinorUnits } from './currencyFormat';
export { formatMinorUnits } from './currencyFormat';
```

Импорт-строку `import { formatMinorUnits } from './currencyFormat';` добавить к остальным импортам сверху файла. Реэкспорт-строку поставить на место удалённой функции.

- [ ] **Step 5: Убрать осиротевший импорт `formatLocaleNumber`, если он больше не используется**

Проверить: `grep -n "formatLocaleNumber" src/AFK4.Operator.App.Web/src/operatorHelpers.ts`
Если осталось только в строке импорта — отредактировать импорт
`import { formatNumber as formatLocaleNumber, formatDateParts } from '@afk4/formatting';`
на `import { formatDateParts } from '@afk4/formatting';`
(`minorToMajor` оставить — он ещё нужен `formatMoneyInputMinorUnits`.)
Финальная проверка типов в Step 7 поймает, если что-то осталось.

- [ ] **Step 6: Обновить рендер-ассерты, сломавшиеся из-за смены суффикса**

В `src/AFK4.Operator.App.Web/src/App.test.tsx`:

Строка 203: было
```ts
    expect(screen.getAllByText(/USD/).length).toBeGreaterThan(0);
```
стало
```ts
    expect(screen.getAllByText(/\$/).length).toBeGreaterThan(0);
```

Строка 1217: было
```ts
    expect(await screen.findByText('Пополнить депозит: 12 TJS')).toBeInTheDocument();
```
стало
```ts
    expect(await screen.findByText('Пополнить депозит: 12 с.')).toBeInTheDocument();
```

Строка 1309: было
```ts
    fireEvent.click(screen.getByRole('button', { name: /25 TJS/ }));
```
стало
```ts
    fireEvent.click(screen.getByRole('button', { name: /25 с\./ }));
```

- [ ] **Step 7: Запустить тесты и проверку типов**

Run: `~/.bun/bin/bun test src/AFK4.Operator.App.Web/src/currencyFormat.test.ts src/AFK4.Operator.App.Web/src/App.test.tsx`
Expected: PASS.
Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run tsc --noEmit` (или скрипт проверки типов из `package.json`, напр. `bun run typecheck`)
Expected: 0 ошибок.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/currencyFormat.ts src/AFK4.Operator.App.Web/src/currencyFormat.test.ts src/AFK4.Operator.App.Web/src/operatorHelpers.ts src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(operator): format money with currency sign via shared currencyFormat module"
```

---

## Task 3: Плитка зала (`floorMapState`) через единый форматтер

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/floorMapState.ts:190-197` (`accruedCostText`) + импорты сверху
- Modify: `src/AFK4.Operator.App.Web/src/floorMapState.test.ts:75`

- [ ] **Step 1: Обновить тест ожидаемого вывода постоплаты**

В `src/AFK4.Operator.App.Web/src/floorMapState.test.ts` строка 75: было
```ts
      remaining: '≈ 22.50 TJS',
```
стало
```ts
      remaining: '≈ 22,5 с.',
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `~/.bun/bin/bun test src/AFK4.Operator.App.Web/src/floorMapState.test.ts`
Expected: FAIL — фактическое `'≈ 22.50 TJS'` ≠ ожидаемое `'≈ 22,5 с.'`.

- [ ] **Step 3: Переключить `accruedCostText` на единый форматтер**

В `floorMapState.ts` добавить импорт к существующим (рядом с `import { minorToMajor } from '@afk4/money';`):

```ts
import { formatMinorUnits } from './currencyFormat';
```

Заменить тело `accruedCostText` (строки 190-197): было
```ts
function accruedCostText(minorUnits: number | null, currencyCode: string | null, t: TFn): string {
  if (minorUnits === null) {
    return t('op.floor.remaining.playing');
  }

  const amount = minorToMajor(minorUnits).toFixed(2);
  return currencyCode ? `≈ ${amount} ${currencyCode}` : `≈ ${amount}`;
}
```
стало
```ts
function accruedCostText(minorUnits: number | null, currencyCode: string | null, t: TFn): string {
  if (minorUnits === null) {
    return t('op.floor.remaining.playing');
  }

  return currencyCode
    ? `≈ ${formatMinorUnits(minorUnits, currencyCode)}`
    : `≈ ${minorToMajor(minorUnits).toFixed(2)}`;
}
```

(`minorToMajor` остаётся импортирован — он нужен в no-currency фолбэке.)

> Примечание: декоративная отделка плитки (стрелка «▲», точное место суммы) — это §4 и делается в Этапе «Карта», не здесь. Здесь только знак валюты и единый формат.

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `~/.bun/bin/bun test src/AFK4.Operator.App.Web/src/floorMapState.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorMapState.ts src/AFK4.Operator.App.Web/src/floorMapState.test.ts
git commit -m "feat(operator): seat accrued-cost uses shared currency formatter"
```

---

## Task 4: Вычистить inline `0 ${currencyCode}` обходы форматтера

**Контекст:** в нескольких воркспейсах нулевой/пустой случай рисуется хардкодом `` `0 ${currencyCode}` `` в обход форматтера — после Task 2 рядом с «54 с.» появилось бы «0 TJS». `formatMoney(value, fallback)` уже корректно отдаёт ноль в нужной валюте для `null`, поэтому заменяем паттерн на него (полу-наличие хуже отсутствия, §32/§33 спека-линз).

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts:1292`

- [ ] **Step 1: Найти все обходы**

Run: `grep -rn "0 \${currencyCode}" src/AFK4.Operator.App.Web/src --include=*.tsx --include=*.ts`
Expected: совпадения в `BackendPaymentsWorkspace.tsx` (большинство), `BackendPosWorkspace.tsx`, `BackendPlayersWorkspace.tsx`. (Случай в `floorMapState.ts` уже закрыт Task 3; случай в `operatorHelpers.ts:842` удалён в Task 2.)

- [ ] **Step 2: Заменить по правилу (тернарник → `formatMoney`)**

Правило трансформации для каждого совпадения вида
```tsx
MONEY ? formatMinorUnits(MONEY.minorUnits, MONEY.currencyCode) : `0 ${currencyCode}`
```
заменить на
```tsx
formatMoney(MONEY, currencyCode)
```
где `MONEY` — конкретное выражение в этой строке (напр. `cashIn`, `netSales`, `refunds`, `difference`, `grossSales`, `expectedCash`).

Конкретные примеры из `BackendPaymentsWorkspace.tsx`:

Строка ~229: было
```tsx
    [t('op.payments.methods.cash'), cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`, t('op.payments.methods.cashShare'), t('op.payments.methods.cashOps', { count: cashRows.length })],
```
стало
```tsx
    [t('op.payments.methods.cash'), formatMoney(cashIn, currencyCode), t('op.payments.methods.cashShare'), t('op.payments.methods.cashOps', { count: cashRows.length })],
```

Строка ~353: было
```tsx
        <StateFlag label={t('op.payments.strip.revenue')} value={grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`} />
```
стало
```tsx
        <StateFlag label={t('op.payments.strip.revenue')} value={formatMoney(grossSales, currencyCode)} />
```

Применить то же правило ко всем совпадениям из Step 1 в трёх воркспейсах.
Не трогать строки, где альтернатива не `` `0 ${currencyCode}` `` (напр. `countedCash ? … : t('op.payments.reconcile.notClosed')` — там осмысленный иной фолбэк).
Убедиться, что `formatMoney` импортирован в каждом изменённом файле (он уже импортируется во всех трёх — проверить шапку импортов; если в каком-то файле после правок `formatMinorUnits` больше не используется, убрать его из импорта — tsc подскажет).

- [ ] **Step 3: Вычистить dev-mock фикстуру**

В `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` строка 1292: было
```ts
    { name: 'Amir K.', status: 'active', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: `120 ${currencyCode}`, phoneNumber: '', source: 'fixture' },
```
стало
```ts
    { name: 'Amir K.', status: 'active', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: formatMinorUnits(12000, currencyCode), phoneNumber: '', source: 'fixture' },
```

(`formatMinorUnits` уже доступен в `operatorHelpers.ts` через реэкспорт из Task 2.)

- [ ] **Step 4: Проверить, что обходов не осталось**

Run: `grep -rn "0 \${currencyCode}" src/AFK4.Operator.App.Web/src --include=*.tsx --include=*.ts`
Expected: пусто (0 совпадений).

- [ ] **Step 5: Прогнать весь фронт-тест и типы**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test`
Expected: PASS — весь сьют зелёный.
Run: `~/.bun/bin/bun run tsc --noEmit` (или `bun run typecheck`)
Expected: 0 ошибок.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/operatorHelpers.ts
git commit -m "fix(operator): route zero-money fallbacks through formatMoney (no ISO leak)"
```

---

## Task 5: Финальная проверка и сборка

**Files:** нет правок — только верификация (§10 спека).

- [ ] **Step 1: Полный прогон тестов фронта и money-пакета**

Run: `~/.bun/bin/bun test packages/money/src/index.test.ts && cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test`
Expected: всё зелёное.

- [ ] **Step 2: Проверка типов и сборка**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run tsc --noEmit && ~/.bun/bin/bun run build`
Expected: 0 ошибок типов; сборка успешна.

- [ ] **Step 3: Глаз-контроль критерия §10 — ISO-код не утёк в UI**

Run: `grep -rn "\${currencyCode}\`" src/AFK4.Operator.App.Web/src --include=*.tsx --include=*.ts`
Expected: пусто или только осмысленные не-денежные случаи. Денежных `… ${currencyCode}` суффиксов в JSX/строках быть не должно — все деньги идут через `formatMinorUnits`/`formatMoney`.

- [ ] **Step 4 (опционально, ручная проверка):** запустить dev-mock и глазами увидеть «с.» на плитках/в кассе

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun run dev`
Open: локальный URL → плитки постоплаты показывают «≈ N с.», касса/платежи — суммы со знаком «с.», нигде нет «TJS».

---

## Self-Review

- **Spec coverage (§7):** ISO → знак в одной точке — Task 1+2 ✅; чинит UI везде разом (реэкспорт + единый модуль) — Task 2 ✅; плитка «N с.» — Task 3 ✅; разделители/десятичные «по контексту» — сохранено текущее поведение `formatMinorUnits` (целое→0 знаков, дробное→2); знак из мапы — Task 1 (в `@afk4/money` вместо i18n-json, обосновано выше) ✅. §10 критерий «ни одного ISO в money UI» — Task 4 (чистка обходов) + Task 5 Step 3 ✅.
- **Известный нюанс (вне скоупа):** числовая локаль захардкожена `'ru-RU'` в `formatMinorUnits`; дробные показываются как «22,5», а не «22,50». Это текущее поведение для всех денег — не регрессия. Если нужно «всегда 2 знака для дробных» или локаль по языку UI — отдельная однострочная правка `formatMinorUnits`, не входит в этот PR.
- **Placeholder scan:** код приведён во всех шагах; Task 4 — правило + 2 конкретных примера + самопроверяющий `grep` на 0 совпадений (не плейсхолдер, а верифицируемое конечное состояние).
- **Type consistency:** `formatMinorUnits(minorUnits: number, currencyCode: string)` и `formatMoney(value, fallbackCurrencyCode)` — сигнатуры неизменны; `currencySymbol(currencyCode: string): string` единообразен в Task 1/2/4.
