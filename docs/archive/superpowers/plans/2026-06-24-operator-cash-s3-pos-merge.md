# Operator «Касса» S3 — слияние «Продажи»+«Заказы» в единый POS + полировка

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Свести вкладки `sales` (POS) и `orders` (заказы магазина) в одну вкладку «Продажи» с сегмент-переключателем «Касса | Заказы» (целевые 3 вкладки вместо 4), без новой бэк-логики; плюс финальная полировка (CSS нового лейаута, харднинг CSV-экспорта).

**Architecture:** Новый воркспейс-хост `cash/CashSalesWorkspace.tsx` владеет состоянием сегмента (`'pos' | 'orders'`), гейтит сегменты по правам и рендерит `BackendPosWorkspace` и `ShopOrdersWorkspace` во **встроенном режиме** (`embedded`) — точно тем же приёмом, что `ReviewWorkspace` в S2 (корень `<section>` вместо `<main>`, своя `screen-head` скрыта). `CashTab` сужается до `'sales'|'shift'|'journal'`. POS и Заказы в данных НЕ связаны (заказ = товарный заказ из Player Shell без оплаты, касса лишь меняет статус) — слияние чисто презентационное, оплату не сшиваем.

**Tech Stack:** React + TypeScript, `@afk4/i18n` (ICU, ru/en/tg), `bun test` (happy-dom + jest-dom), Vite/tsc build.

## Global Constraints

(Каждая задача неявно включает эти требования.)

- **Money-path бэка НЕ трогаем.** Никаких изменений в `*.cs`, никаких новых эндпоинтов/миграций. Слияние — только фронт.
- **Деньги:** minor units в DTO; `formatMoney(TJS)` рендерит целые + «с.», разделитель тысяч = **NBSP (U+00A0)**, отрицательное = ASCII-минус. Тесты ассертить под реальный вывод форматтера, не точные строки.
- **i18n:** каждый новый ключ — реально в ru/en/tg; **tg = настоящий таджикский** (касса→«хазина», заказы→«фармоишҳо», продажи→«фурӯш»); guard-тест enforces tg≠ru. После правок locales — регенерация `messages.ts` (`cd packages/i18n && bun run gen`). Гейт i18n: `cd packages/i18n && bun test`.
- **Embedded-паттерн (канон из S2 `ReviewWorkspace`):** проп `embedded = false` (`embedded?: boolean`); собственная `screen-head` оборачивается в `{!embedded && (...)}`; корень — `embedded ? <section className="X-embed">{body}</section> : <main className="workspace-screen X-screen">{body}</main>`. Поведение non-embedded байт-в-байт прежнее.
- **Без регрессии доступа:** объединённая вкладка «Продажи» видна любому, кто видел старую `sales` ИЛИ старую `orders` (т.е. `hasAnyPermission([createPosSale, payPosSale, refundPosSale, voidPosSale])` — `createPosSale` покрывает orders-only-кейс).
- **Гейты фронта:** `bun test` в `src/AFK4.Operator.App.Web` (subdir-прогон); **`App.test.tsx` отдельным `bun test`-вызовом** (утечка `mock.module` process-wide); `bun run build` (tsc+vite — ловит type-ошибки). Команды bun — полным путём `/home/fedya/.bun/bin/bun`.
- **Никаких AI-подписей** в коммитах/коде/PR; никаких секретов.
- **`useToast()` бросает без `ToastProvider`** — юнит-тесты `cash/*` обёрнуты только в `I18nProvider`. Ошибки экспорта сурфейсить локальным inline-нотисом (`role="alert"`), НЕ тостом.

**Базовые факты (проверено):**
- `permissionNames`: `createPosSale='pos.sales.create'`, `payPosSale='pos.sales.pay'`, `refundPosSale='pos.sales.refund'`, `voidPosSale='pos.sales.void'`.
- `hasAnyPermission(session, string[])` из `../operatorPermissions`.
- POS (`BackendPosWorkspace`) НЕ использует `useToast` (локальный `feedback`); Orders (`ShopOrdersWorkspace`) использует `useToast`.

---

### Task 1: i18n — ключи `op.cash.sales.*` + чистка осиротевших nav-ключей

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Generate: `packages/i18n/src/messages.ts` (через `bun run gen`)

**Interfaces:**
- Produces: ключи `op.cash.sales.tab`, `op.cash.sales.segPos`, `op.cash.sales.segOrders` (потребляют Task 4 и Task 5).

Контекст: новая вкладка «Продажи» = сегменты «Касса» (POS) и «Заказы». Существующие `op.shell.nav.pos`/`op.shell.nav.shop_orders` после рерайтинга (Task 5) станут осиротевшими — заменяем их новым namespace `op.cash.sales.*` (как `op.cash.journal.seg*` в S2).

- [ ] **Step 1: Добавить 3 ключа в каждый locale.** В `locales/ru.json` сразу после строки `"op.cash.journal.segReview": "Проверка",` (рядом с группой `op.cash.journal.*`, ~строка 992) вставить:

```json
  "op.cash.sales.tab": "Продажи",
  "op.cash.sales.segPos": "Касса",
  "op.cash.sales.segOrders": "Заказы",
```

В `locales/en.json` в аналогичном месте (после `"op.cash.journal.segReview": "Review",`):

```json
  "op.cash.sales.tab": "Sales",
  "op.cash.sales.segPos": "Register",
  "op.cash.sales.segOrders": "Orders",
```

В `locales/tg.json` (после `"op.cash.journal.segReview": "Санҷиш",`) — реальный таджикский:

```json
  "op.cash.sales.tab": "Фурӯш",
  "op.cash.sales.segPos": "Хазина",
  "op.cash.sales.segOrders": "Фармоишҳо",
```

- [ ] **Step 2: Проверить, что `op.shell.nav.pos` и `op.shell.nav.shop_orders` больше нигде не используются.**

Run: `cd /home/fedya/projects/afk4.net && grep -rn "op.shell.nav.pos\|op.shell.nav.shop_orders" src/ packages/ locales/`
Ожидание: единственное упоминание — определения в трёх `locales/*.json` (и в сгенерённом `messages.ts`). Использований в `.tsx`/`.ts` быть НЕ должно (CashWorkspace перейдёт на `op.cash.sales.tab` в Task 5 — но Task 1 идёт раньше, поэтому на этом шаге CashWorkspace ещё ссылается на `op.shell.nav.pos`/`shop_orders`).

**ВАЖНО:** Раз Task 1 идёт до Task 5, `op.shell.nav.pos`/`shop_orders` пока используются в `CashWorkspace.tsx`. Поэтому **на этом шаге ключи НЕ удалять.** Удаление перенесено в Task 5 (после того как CashWorkspace перестанет их использовать). Здесь только убеждаемся, что иных потребителей нет (grep выше должен показать только `CashWorkspace.tsx` среди `.tsx`).

- [ ] **Step 3: Регенерировать messages.ts.**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen`
Ожидание: `messages.ts` обновлён, новые ключи присутствуют.

- [ ] **Step 4: Прогнать i18n-гейт (паритет + tg≠ru).**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test`
Ожидание: PASS (все три locale имеют новые ключи; tg отличается от ru).

- [ ] **Step 5: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add locales packages/i18n && git commit -m "i18n(cash-s3): ключи op.cash.sales.* для вкладки Продажи"
```

---

### Task 2: `BackendPosWorkspace` — встроенный режим (`embedded`) + первый smoke-тест

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx` (signature ~86, root ~719-1109)
- Create: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`

**Interfaces:**
- Produces: `BackendPosWorkspace` принимает `embedded?: boolean`. При `embedded` корень = `<section className="pos-screen pos-embed">` без `screen-head`.

Контекст: POS — главный воркспейс вкладки «Продажи». Сейчас рендерит собственный `<main className="workspace-screen pos-screen">` с `screen-head` (заголовок «Продажи»). Во встроенном режиме заголовок даёт сегмент «Касса», поэтому собственную `screen-head` скрываем и меняем `<main>`→`<section>`. У POS НЕТ собственного теста (риск из spec) — добавляем smoke.

- [ ] **Step 1: Написать падающий smoke-тест.** Создать `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`:

```tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BackendPosWorkspace } from './BackendPosWorkspace';

afterEach(cleanup);

// backend=null → fixture-режим: каталог/корзина из заглушек, без сетевых запросов.
function renderPos(embedded: boolean) {
  render(
    <I18nProvider initialLocale="ru">
      <BackendPosWorkspace currencyCode="TJS" backend={null} embedded={embedded} />
    </I18nProvider>
  );
}

describe('BackendPosWorkspace', () => {
  it('standalone: рендерит шапку «Продажи» и панели каталог/корзина/оплата', () => {
    renderPos(false);
    expect(screen.getByRole('heading', { name: /Продажи/ })).toBeInTheDocument();
    expect(document.querySelector('main.pos-screen')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    expect(screen.getByText('Оплата')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
  });

  it('embedded: без собственного <main>/heading, корень — section.pos-embed, панели на месте', () => {
    renderPos(true);
    // Заголовок «Продажи» даёт сегмент-вкладка, не сам POS → собственного heading нет.
    expect(screen.queryByRole('heading', { name: /Продажи/ })).toBeNull();
    expect(document.querySelector('main.pos-screen')).toBeNull();
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Оплата')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Прогнать — убедиться, что падает.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/BackendPosWorkspace.test.tsx`
Ожидание: FAIL (компонент пока не принимает `embedded`; embedded-кейс находит `main.pos-screen`).

- [ ] **Step 3: Добавить проп `embedded` в сигнатуру.** В `BackendPosWorkspace.tsx` строка 86 заменить:

```tsx
export function BackendPosWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
```

на:

```tsx
export function BackendPosWorkspace({ currencyCode, backend, embedded = false }: { currencyCode: string; backend: OperatorBackendContext | null; embedded?: boolean }) {
```

- [ ] **Step 4: Сделать корень условным и скрыть `screen-head` во встроенном режиме.** Сразу перед `return (` (строка 719) добавить строку:

```tsx
  const Root = embedded ? 'section' : 'main';
```

Заменить открывающий тег корня (строка 720):

```tsx
    <main className="workspace-screen pos-screen">
```

на:

```tsx
    <Root className={embedded ? 'pos-screen pos-embed' : 'workspace-screen pos-screen'}>
```

Обернуть блок `screen-head` (строки 721-729, `<section className="screen-head pos-head">…</section>`) в `{!embedded && (…)}`:

```tsx
      {!embedded && (
        <section className="screen-head pos-head">
          <div>
            <span>{t('op.pos.title')}</span>
            <h1>{t('op.pos.heading')}</h1>
          </div>
          <div className="screen-actions">
            <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.pos.platformConnected'), t)}</span>
          </div>
        </section>
      )}
```

Заменить закрывающий тег корня (последняя строка return, парная к `<main className="workspace-screen pos-screen">`, ~строка 1109) с `</main>` на `</Root>`.

- [ ] **Step 5: Прогнать smoke-тест — PASS.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/BackendPosWorkspace.test.tsx`
Ожидание: PASS (2/2).

- [ ] **Step 6: Build (tsc ловит `Root`-типизацию и прочее).**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS (если `<Root>` даёт type-ошибку — `const Root: 'section' | 'main' = embedded ? 'section' : 'main';` обе валидные `JSX.IntrinsicElements`; ошибки быть не должно).

- [ ] **Step 7: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx && git commit -m "feat(cash-s3): встроенный режим BackendPosWorkspace + первый smoke-тест"
```

---

### Task 3: `ShopOrdersWorkspace` — встроенный режим (`embedded`)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx` (signature 37, return 115-163)
- Modify: `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx` (добавить embedded-кейс)

**Interfaces:**
- Produces: `ShopOrdersWorkspace` принимает `embedded?: boolean`. При `embedded` корень = `<section className="shop-orders-embed">` без `screen-head`.

- [ ] **Step 1: Добавить падающий embedded-кейс в тест.** В конец `describe(...)` в `ShopOrdersWorkspace.test.tsx` добавить (используя тот же render-враппер с провайдерами, что и существующие кейсы файла — там уже есть `I18nProvider` + `ToastProvider`, т.к. компонент дёргает `useToast`):

```tsx
  it('embedded: корень — section.shop-orders-embed, без собственного <main>', async () => {
    renderShopOrders(null); // тот же helper, что в файле; backend=null → пустая очередь, status ready
    await waitFor(() => expect(document.querySelector('section.shop-orders-embed')).not.toBeNull());
    expect(document.querySelector('main.shop-orders-screen')).toBeNull();
  });
```

Если в файле нет helper'а с пропом `embedded` — добавить локальный рендер с тем же набором провайдеров, что используют существующие тесты, и передать `embedded`. (Опереться на существующий способ рендера в этом файле; не вводить новый стиль.)

- [ ] **Step 2: Прогнать — FAIL.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/ShopOrdersWorkspace.test.tsx`
Ожидание: FAIL (нет `section.shop-orders-embed`; компонент всегда рендерит `main.shop-orders-screen`).

- [ ] **Step 3: Добавить проп + условный корень.** Строка 37 — заменить:

```tsx
export function ShopOrdersWorkspace({ backend }: { backend: OperatorBackendContext | null }) {
```

на:

```tsx
export function ShopOrdersWorkspace({ backend, embedded = false }: { backend: OperatorBackendContext | null; embedded?: boolean }) {
```

Заменить весь блок `return (…)` (строки 115-163) на: вынести тело в `body`-фрагмент со скрытой во встроенном режиме `screen-head`, корень — условный (приём из `ReviewWorkspace`):

```tsx
  const body = (
    <>
      {!embedded && (
        <section className="screen-head">
          <h1>{t('op.shopOrders.title')}</h1>
        </section>
      )}

      {status === 'loading' ? (
        <p className="workspace-loading">{t('op.shopOrders.loading')}</p>
      ) : status === 'error' ? (
        <p className="workspace-error" role="alert">{loadError ?? t('op.shopOrders.error')}</p>
      ) : orders.length === 0 ? (
        <EmptyState title={t('op.shopOrders.empty')} className="shop-orders-empty" />
      ) : (
        <ul className="shop-orders-list">
          {orders.map((order) => (
            <li key={order.id} className="shop-order-card">
              <div className="shop-order-head">
                <strong>{order.playerDisplayName}</strong>
                <span className={`shop-order-status ${order.status}`}>
                  {order.status === 'accepted'
                    ? t('op.shopOrders.status.accepted')
                    : t('op.shopOrders.status.placed')}
                </span>
              </div>
              <div className="shop-order-meta">
                <span>{t('op.shopOrders.seat')} {order.seatId}</span>
                <span>{formatMinorUnits(order.total.minorUnits, order.total.currencyCode)}</span>
              </div>
              <div className="shop-order-actions">
                {order.status === 'placed' && (
                  <button type="button" onClick={runAction(order, 'accept')}>
                    {t('op.shopOrders.accept')}
                  </button>
                )}
                {order.status === 'accepted' && (
                  <button type="button" onClick={runAction(order, 'deliver')}>
                    {t('op.shopOrders.deliver')}
                  </button>
                )}
                <button type="button" className="shop-order-cancel" onClick={runAction(order, 'cancel')}>
                  {t('op.shopOrders.cancel')}
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </>
  );

  return embedded
    ? <section className="shop-orders-embed">{body}</section>
    : <main className="workspace-screen shop-orders-screen">{body}</main>;
```

- [ ] **Step 4: Прогнать тест — PASS.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/ShopOrdersWorkspace.test.tsx`
Ожидание: PASS (все прежние кейсы + новый embedded).

- [ ] **Step 5: Build.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS.

- [ ] **Step 6: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx && git commit -m "feat(cash-s3): встроенный режим ShopOrdersWorkspace"
```

---

### Task 4: `CashSalesWorkspace` — хост вкладки «Продажи» с сегментами «Касса | Заказы»

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashSalesWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/cash/CashSalesWorkspace.test.tsx`

**Interfaces:**
- Consumes: `BackendPosWorkspace` (`embedded` из Task 2), `ShopOrdersWorkspace` (`embedded` из Task 3), ключи `op.cash.sales.segPos/segOrders` (Task 1).
- Produces: `CashSalesWorkspace({ backend, currencyCode, session })` — потребляет Task 5 (CashWorkspace).

Контекст: точная калька `cash/CashJournalWorkspace.tsx` (S2): сегменты гейтятся правами, бар скрыт при одном сегменте, активный сегмент рендерит встроенный воркспейс. Отличие: у `CashSalesWorkspace` НЕТ собственной `screen-head` (POS — плотный воркспейс, заголовок даёт вкладка «Продажи»).

- [ ] **Step 1: Написать падающий тест.** Создать `src/AFK4.Operator.App.Web/src/cash/CashSalesWorkspace.test.tsx`:

```tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { CashSalesWorkspace } from './CashSalesWorkspace';

afterEach(cleanup);

// backend=null → POS в fixture-режиме, очередь заказов пустая. ToastProvider обязателен:
// сегмент «Заказы» рендерит ShopOrdersWorkspace, который дёргает useToast.
function renderSales(permissions: string[]) {
  const session = { permissions, organizationId: 'o' } as never;
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashSalesWorkspace backend={null} currencyCode="TJS" session={session} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashSalesWorkspace', () => {
  it('полные права POS: видны оба сегмента, по умолчанию «Касса» (POS)', () => {
    renderSales(['pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void']);
    expect(screen.getByRole('tab', { name: 'Касса' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Заказы' })).toBeInTheDocument();
    // POS-панель активна по умолчанию.
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
  });

  it('только pay (без create): сегмент «Заказы» скрыт, бар не показан, POS отрисован', () => {
    renderSales(['pos.sales.pay']);
    expect(screen.queryByRole('tab', { name: 'Заказы' })).toBeNull();
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
  });

  it('переключение на «Заказы» рендерит встроенный ShopOrdersWorkspace вместо POS', async () => {
    renderSales(['pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void']);
    fireEvent.click(screen.getByRole('tab', { name: 'Заказы' }));
    await waitFor(() => expect(document.querySelector('section.shop-orders-embed')).not.toBeNull());
    expect(document.querySelector('section.pos-embed')).toBeNull();
  });
});
```

- [ ] **Step 2: Прогнать — FAIL.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashSalesWorkspace.test.tsx`
Ожидание: FAIL (модуль `./CashSalesWorkspace` не существует).

- [ ] **Step 3: Создать `cash/CashSalesWorkspace.tsx`:**

```tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';

type SalesSegment = 'pos' | 'orders';

// Вкладка «Продажи» = POS («Касса») + очередь заказов магазина («Заказы») как независимые
// под-режимы. В данных они не связаны (заказ из Player Shell без оплаты, касса лишь меняет
// статус) — поэтому сегмент-переключатель, а не сшивка оплаты. Сегменты гейтятся правами.
export function CashSalesWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const canPos = hasAnyPermission(session, [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale
  ]);
  const canOrders = hasAnyPermission(session, [permissionNames.createPosSale]);

  const segments: { id: SalesSegment; label: string }[] = [];
  if (canPos) segments.push({ id: 'pos', label: t('op.cash.sales.segPos') });
  if (canOrders) segments.push({ id: 'orders', label: t('op.cash.sales.segOrders') });

  const [active, setActive] = useState<SalesSegment>(() => segments[0]?.id ?? 'pos');

  return (
    <main className="workspace-screen cash-sales-screen">
      {segments.length > 1 && (
        <div className="cash-sales-segments" role="tablist" aria-label={t('op.cash.sales.tab')}>
          {segments.map((segment) => (
            <button
              key={segment.id}
              type="button"
              role="tab"
              aria-selected={active === segment.id}
              className={active === segment.id ? 'active' : undefined}
              onClick={() => setActive(segment.id)}
            >
              {segment.label}
            </button>
          ))}
        </div>
      )}

      {active === 'pos' && canPos && (
        <BackendPosWorkspace currencyCode={currencyCode} backend={backend} embedded />
      )}
      {active === 'orders' && canOrders && (
        <ShopOrdersWorkspace backend={backend} embedded />
      )}
    </main>
  );
}
```

- [ ] **Step 4: Прогнать тест — PASS.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashSalesWorkspace.test.tsx`
Ожидание: PASS (3/3).

- [ ] **Step 5: Build.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS.

- [ ] **Step 6: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/cash/CashSalesWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/CashSalesWorkspace.test.tsx && git commit -m "feat(cash-s3): CashSalesWorkspace — вкладка Продажи с сегментами Касса/Заказы"
```

---

### Task 5: Рерайтинг — `CashTab` сужается, `CashWorkspace` рендерит `CashSalesWorkspace`, чистка nav-ключей

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashTabBar.tsx` (строка 1, union)
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashTabBar.test.tsx` (строка 9, sample)
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.ts` (CASH_TAB_PERMISSIONS, CASH_TAB_ORDER)
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx` (импорты, allTabs, render)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (удалить осиротевшие nav-ключи)
- Generate: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes: `CashSalesWorkspace` (Task 4), `op.cash.sales.tab` (Task 1).

Контекст: после слияния вкладок остаётся 3: `sales`/`shift`/`journal`. `sales` теперь хостит POS+Заказы. Права `sales` = суперсет POS-прав (`createPosSale` покрывает orders-only-кейс → без регрессии доступа).

- [ ] **Step 1: Сузить тип `CashTab`.** `cash/CashTabBar.tsx` строка 1:

```tsx
export type CashTab = 'sales' | 'orders' | 'shift' | 'journal';
```

→

```tsx
export type CashTab = 'sales' | 'shift' | 'journal';
```

- [ ] **Step 2: Поправить sample в `CashTabBar.test.tsx`.** Строки 8-9 — убрать sample `orders`:

```tsx
  { id: 'sales', label: 'Продажи' },
  { id: 'orders', label: 'Заказы' },
```

→

```tsx
  { id: 'sales', label: 'Продажи' },
  { id: 'shift', label: 'Смена' },
```

(Если `shift` уже есть в массиве ниже — не дублировать; просто удалить строку с `orders`. Цель: в массиве нет `id: 'orders'`, и есть ≥2 валидных таба.)

- [ ] **Step 3: Обновить `cashModel.ts`.** Убрать ключ `orders` из `CASH_TAB_PERMISSIONS` (строка 19) и из `CASH_TAB_ORDER` (строка 24):

```tsx
const CASH_TAB_PERMISSIONS: Record<CashTab, readonly string[]> = {
  sales: [permissionNames.createPosSale, permissionNames.payPosSale, permissionNames.refundPosSale, permissionNames.voidPosSale],
  shift: [permissionNames.viewShift, permissionNames.openShift, permissionNames.closeShift, permissionNames.manageShiftCash, permissionNames.viewReports],
  journal: [permissionNames.approveMoneyAction, permissionNames.viewReports, permissionNames.viewShift, permissionNames.manageShiftCash]
};

const CASH_TAB_ORDER: CashTab[] = ['sales', 'shift', 'journal'];
```

- [ ] **Step 4: Обновить `CashWorkspace.tsx`.** Заменить импорты POS+Orders на `CashSalesWorkspace` (строки 8-9):

```tsx
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';
```

→

```tsx
import { CashSalesWorkspace } from './CashSalesWorkspace';
```

Обновить `allTabs` (строки 29-34): убрать `orders`-строку, заменить label `sales`:

```tsx
  const allTabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.cash.sales.tab') },
    { id: 'shift', label: t('op.cash.tab.shift') },
    { id: 'journal', label: t('op.cash.journal.tab') }
  ];
```

Обновить рендер-блок (строки 48-49): заменить рендер POS и убрать ветку orders:

```tsx
        {activeTab === 'sales' && <CashSalesWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
```

(строку `{activeTab === 'orders' && <ShopOrdersWorkspace backend={backend} />}` удалить целиком.)

- [ ] **Step 5: Удалить осиротевшие nav-ключи.** Теперь `op.shell.nav.pos`/`op.shell.nav.shop_orders` нигде не используются. Сначала подтвердить:

Run: `cd /home/fedya/projects/afk4.net && grep -rn "op.shell.nav.pos\|op.shell.nav.shop_orders" src/ packages/i18n/src/`
Ожидание: ноль совпадений в `src/` и `packages/i18n/src/` (кроме сгенерённого `messages.ts`, который перезапишем).

Удалить строки `"op.shell.nav.pos": …` и `"op.shell.nav.shop_orders": …` из `locales/ru.json`, `locales/en.json`, `locales/tg.json`.

- [ ] **Step 6: Регенерировать messages.ts.**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen && /home/fedya/.bun/bin/bun test`
Ожидание: PASS (паритет сохранён — ключи удалены во всех трёх locale).

- [ ] **Step 7: Прогнать cash-юниты + build.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/ && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS (CashTabBar.test обновлён; тип `CashTab` консистентен; build чистый).

- [ ] **Step 8: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/cash locales packages/i18n && git commit -m "refactor(cash-s3): слияние вкладок sales+orders в Продажи, чистка nav-ключей"
```

---

### Task 6: CSS — лейаут вкладки «Продажи», встроенные режимы, нотис экспорта

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/21-cash.css`

**Interfaces:**
- Consumes: классы `.cash-sales-screen`, `.cash-sales-segments` (Task 4), `.pos-embed` (Task 2), `.shop-orders-embed` (Task 3), `.cash-export-error` (Task 8).

Контекст: сегмент-бар «Продажи» = тот же вид, что `.cash-journal-segments` (S2). Встроенные режимы убирают паддинги/скролл уровня страницы (их даёт хост-вкладка `.cash-tab-content`).

- [ ] **Step 1: Дописать правила в конец `21-cash.css`** (использовать существующие токены `--space-*`, как в соседних правилах файла):

```css
/* S3: вкладка «Продажи» = сегменты «Касса | Заказы» + встроенные POS/Заказы */
.cash-sales-screen {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

/* Сегмент-бар идентичен журналу кассы (единый дизайн-стандарт) */
.cash-sales-segments {
  display: flex;
  gap: var(--space-2);
}
.cash-sales-segments button[role='tab'] {
  padding: var(--space-1) var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}
.cash-sales-segments button[role='tab'].active {
  background: var(--color-surface-raised);
  color: var(--color-text);
  border-color: var(--color-accent);
}

/* Встроенные режимы: без паддинга/скролла уровня страницы — их даёт .cash-tab-content */
.pos-embed,
.shop-orders-embed {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

/* Нотис ошибки экспорта CSV (Task 8) — компактный, не разрушающий лейаут */
.cash-export-error {
  margin: 0;
  color: var(--color-danger);
  font-size: var(--font-sm);
}
```

**ВАЖНО:** перед коммитом сверить имена токенов с фактическими в `21-cash.css`/`@afk4/tokens` (например `--color-border`, `--radius-sm`, `--color-danger`, `--font-sm`). Если какого-то токена нет — взять ближайший существующий, использованный в соседних правилах файла. Не вводить новые произвольные значения мимо токен-шкалы.

- [ ] **Step 2: Build (CSS импортируется через styles).**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS.

- [ ] **Step 3: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/styles/21-cash.css && git commit -m "style(cash-s3): лейаут вкладки Продажи + встроенные режимы POS/Заказы"
```

---

### Task 7: `App.test` — миграция под встроенный POS

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

Контекст: POS теперь встроен (без собственного heading «Продажи»). Два места ассертят heading `Продажи` (строки 790 и 848-849) и структуру `pos-head` (851-854) — это сломается. Навигация `gotoWorkspace('Продажи')` остаётся валидной (метка вкладки = `op.cash.sales.tab` = «Продажи»). Вкладку `orders` App.test нигде не навигирует (проверено), поэтому иных правок нет.

- [ ] **Step 1: Заменить heading-ассерт в тесте навигации (строка 790).**

```tsx
    gotoWorkspace('Продажи');
    expect(screen.getByRole('heading', { name: /Продажи/ })).toBeInTheDocument();
```

→

```tsx
    gotoWorkspace('Продажи');
    // POS встроен в сегмент «Касса» — собственного heading нет; убеждаемся, что POS отрисован.
    expect(await screen.findByText('Каталог')).toBeInTheDocument();
```

- [ ] **Step 2: Переписать детальный POS-блок (строки 848-860).** Заменить:

```tsx
    gotoWorkspace('Продажи');
    const posHead = screen.getByRole('heading', { name: /Продажи/ }).closest('.screen-head');
    expect(posHead).toBeInTheDocument();
    expect(posHead).not.toHaveTextContent('Продажа');
    expect(posHead).not.toHaveTextContent('Возврат');
    expect(posHead).not.toHaveTextContent('Склад');
    expect(posHead).not.toHaveTextContent('История');
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    expect(screen.getByText('Оплата')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
    expect(screen.getByText('Последние чеки')).toBeInTheDocument();
    expect(screen.getByText('Быстрые операции')).toBeInTheDocument();
```

на:

```tsx
    gotoWorkspace('Продажи');
    // Вкладка «Продажи»: сегмент «Касса» (POS) активен по умолчанию, POS встроен (section.pos-embed,
    // без собственной screen-head). Проверяем сегмент-бар + панели POS.
    expect(screen.getByRole('tab', { name: 'Касса' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Заказы' })).toBeInTheDocument();
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    expect(screen.getByText('Оплата')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
    expect(screen.getByText('Последние чеки')).toBeInTheDocument();
    expect(screen.getByText('Быстрые операции')).toBeInTheDocument();
    // Переключение на сегмент «Заказы» показывает встроенную очередь вместо POS.
    fireEvent.click(screen.getByRole('tab', { name: 'Заказы' }));
    await waitFor(() => expect(document.querySelector('section.shop-orders-embed')).not.toBeNull());
    expect(document.querySelector('section.pos-embed')).toBeNull();
```

(Убедиться, что `fireEvent` и `waitFor` импортированы в App.test — они почти наверняка уже есть; если нет — добавить в существующий импорт из `@testing-library/react`.)

- [ ] **Step 3: Прогнать App.test отдельным прогоном.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Ожидание: PASS (полный набор, 0 «Unhandled error»). Если сессия по умолчанию в этом тесте не даёт `pos.sales.create` — сегмент «Заказы» не появится; в этом случае использовать сессию с полным набором POS-прав ИЛИ убрать ассерт на «Заказы»-таб/переключение (оставив POS-панели). Сверить с фактической сессией теста (`installSessionBridge()` / `createSession`).

- [ ] **Step 4: Build.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS.

- [ ] **Step 5: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/App.test.tsx && git commit -m "test(cash-s3): App.test под встроенный POS + сегмент Заказы"
```

---

### Task 8: Харднинг CSV-экспорта (долг S2) — try/catch + inline-нотис

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx` (exportCsv ~80-92, рендер кнопок ~149-151)
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx` (exportCsv ~69-74, рендер ~94-97)
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.test.tsx` (кейс на ошибку)
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.test.tsx` (кейс на ошибку)

Контекст: `exportCsv` в обоих компонентах не обёрнут в try/catch — отклонённый промис уходит в `void` и теряется без обратной связи. Surf'ить ошибку локальным `role="alert"`-нотисом (НЕ тостом — нет `ToastProvider` в юнит-тестах). Сообщение берём из `projectOperatorError(error, t).detail` (уже локализовано).

**Приём для теста провала (без мок-инфраструктуры):** `exportCsv` строит боевой клиент из `backend.config` на клике (не из инъекта). Фейковый `backend` с `config: 'x'` роняет `createAuthenticatedOperatorClients` на построении («Invalid URL»). Если при этом передать инъект-`reports`/`revenueClient` (чтобы первичная загрузка прошла, а не строила клиент на рендере), то компонент отрендерится нормально, а клик по «Экспорт» уйдёт в `catch` → покажет нотис. Это настоящая проверка try/catch, без `mock.module`.

- [ ] **Step 1: Написать падающий кейс для `CashOperationsLedger`.** В `CashOperationsLedger.test.tsx` добавить (используя стиль рендера файла; инъект-`reports` отдаёт пустые строки, поэтому загрузка проходит без боевого клиента):

```tsx
  it('провал экспорта показывает inline-нотис ошибки', async () => {
    // config:'x' роняет построение боевого клиента в exportCsv → попадает в catch.
    const backend = { config: 'x', session: 's', branchId: 'b' } as never;
    render(
      <I18nProvider initialLocale="ru">
        <CashOperationsLedger
          backend={backend}
          branchId="b"
          currencyCode="TJS"
          reports={{ getCashOperationReport: async () => ({ rows: [] }) }}
        />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Кассовых операций нет')).toBeInTheDocument());
    expect(document.querySelector('.cash-export-error')).toBeNull(); // в норме нотиса нет
    fireEvent.click(screen.getByRole('button', { name: /Экспорт/i }));
    await waitFor(() => expect(document.querySelector('.cash-export-error')).not.toBeNull());
  });
```

(Импорты `render/screen/fireEvent/waitFor` из `@testing-library/react`, `I18nProvider` из `@afk4/i18n` — добавить, если в файле ещё нет. Текст пустого состояния «Кассовых операций нет» = `op.cash.journal.empty` ru; кнопка экспорта несёт текст `op.cash.journal.export` — сверить точный матч по реальному рендеру.)

- [ ] **Step 2: Прогнать — FAIL** (нет `.cash-export-error`, т.к. exportCsv без catch роняет необработанный промис).

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashOperationsLedger.test.tsx`

- [ ] **Step 3: Обернуть `exportCsv` в `CashOperationsLedger.tsx`.** Добавить состояние ошибки (рядом с `loadError`, ~строка 45):

```tsx
  const [exportError, setExportError] = useState<string | null>(null);
```

Заменить `exportCsv` (строки 69-74):

```tsx
  const exportCsv = async () => {
    if (backend === null) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    downloadTextFile(`afk4-cash-operations-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 200 }), 'text/csv;charset=utf-8');
  };
```

на:

```tsx
  const exportCsv = async () => {
    if (backend === null) return;
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      downloadTextFile(`afk4-cash-operations-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 200 }), 'text/csv;charset=utf-8');
      setExportError(null);
    } catch (error) {
      setExportError(projectOperatorError(error, t).detail);
    }
  };
```

Отрендерить нотис после строки поиска (после `</div>` блока `.cash-ledger-search`, ~строка 97):

```tsx
      {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
```

- [ ] **Step 3b: Падающий кейс для `CashShiftWorkspace`.** В `CashShiftWorkspace.test.tsx` добавить (инъект `revenueClient`+`reports`, чтобы первичная загрузка прошла без боевого клиента; `config:'x'` роняет экспорт):

```tsx
  it('провал экспорта показывает inline-нотис ошибки', async () => {
    const backend = { config: 'x', session: 's', branchId: 'b' } as never;
    const empty = {
      current: async () => null,
      history: async () => ({ shifts: [], limit: 20 })
    };
    render(
      <I18nProvider initialLocale="ru">
        <CashShiftWorkspace
          backend={backend}
          branchId="b"
          currencyCode="TJS"
          revenueClient={empty}
          reports={{ getCashOperationReport: async () => ({ rows: [] }) }}
        />
      </I18nProvider>
    );
    // дождаться окончания загрузки (кнопки экспорта появляются после загрузки)
    const exportBtn = await screen.findByRole('button', { name: /экспорт сводки смены/i });
    expect(document.querySelector('.cash-export-error')).toBeNull();
    fireEvent.click(exportBtn);
    await waitFor(() => expect(document.querySelector('.cash-export-error')).not.toBeNull());
  });
```

(Имя кнопки = `op.cash.shift.exportShiftSummary` — сверить точный ru-текст по реальному рендеру и поправить regex. Если в файле уже есть render-helper с этими инъектами — использовать его.)

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftWorkspace.test.tsx` → Ожидание: FAIL.

- [ ] **Step 4: Обернуть `exportCsv` в `CashShiftWorkspace.tsx`.** Добавить состояние (рядом с `loadError`, ~строка 57):

```tsx
  const [exportError, setExportError] = useState<string | null>(null);
```

Заменить тело `exportCsv` (строки 80-92), обернув все три ветки в общий try/catch:

```tsx
  const exportCsv = async (kind: 'shifts' | 'cash' | 'sales') => {
    if (backend === null) return;
    try {
      // Клиент строится lazy, только при клике — не на рендере.
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (kind === 'shifts') {
        downloadTextFile(`afk4-shift-summary-${stamp}.csv`, await clients.shifts.exportShiftReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
      } else if (kind === 'cash') {
        downloadTextFile(`afk4-cash-movements-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
      } else {
        downloadTextFile(`afk4-check-list-${stamp}.csv`, await clients.shifts.exportSalesReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
      }
      setExportError(null);
    } catch (error) {
      setExportError(projectOperatorError(error, t).detail);
    }
  };
```

Отрендерить нотис рядом с группой кнопок экспорта (после контейнера кнопок на строках 149-151 — добавить после закрывающего тега их обёртки):

```tsx
      {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
```

(Найти div/секцию, оборачивающую три кнопки экспорта, и поставить нотис сразу после неё.)

- [ ] **Step 5: Прогнать cash-юниты + build.**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/ && /home/fedya/.bun/bin/bun run build`
Ожидание: PASS.

- [ ] **Step 6: Commit.**

```bash
cd /home/fedya/projects/afk4.net && git add src/AFK4.Operator.App.Web/src/cash && git commit -m "fix(cash-s3): харднинг CSV-экспорта — try/catch + inline-нотис ошибки"
```

---

## Финальная проверка (после всех задач)

- Полный фронт subdir: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/` (исключая App.test, либо как настроено в `bun run test`).
- App.test отдельно: `/home/fedya/.bun/bin/bun test src/App.test.tsx` — прогнать несколько раз (флак-проверка контроллером).
- i18n: `cd packages/i18n && /home/fedya/.bun/bin/bun test`.
- Build: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`.
- Бэк не трогали — `dotnet test` не требуется (но CI прогонит).
- Финальное whole-branch ревью (opus), затем PR + зелёный CI + мерж.
