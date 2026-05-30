# Club Монетизация — Товары (Plan 5b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the "Товары" placeholder in the Монетизация screen with a real POS-catalog tab — list products, create a category, create a product, edit a product (incl. deactivate via `isActive`) — on existing backend endpoints, honestly handling the backend's category limitations.

**Architecture:** Reuses the shared `money.ts` minor↔major helper from plan 5a. A pure `catalogModel.ts` maps `PosProductDto` to display rows, derives the category list (the backend has no category list/name endpoint, so categories come from the products' `categoryId` plus categories created this session), and builds the create/update request bodies. A load-only `useCatalog` hook returns the products in a discriminated-union state with `retry`. `CategoryCreateDialog` creates a category (returning it to the tab for the session list); `ProductFormDialog` creates/edits a product. `CatalogTab` ties them together with read-only gating. The tab is rendered in `MonetizationScreen`'s Товары tab, gated by a new `canManageCatalog` flag threaded from the session.

**Tech Stack:** React 19 + TypeScript, Vite, Vitest 4 + jsdom + @testing-library/react (`globals: false` → import `it`/`expect`/`vi` from `'vitest'` per test file), shadcn/ui primitives under `src/components/ui/`, Tailwind v4, i18n RU primary / EN secondary. npm cwd: `D:\afk4.net\src\AFK4.Platform.Web`. Path alias `@/` → `src/`. `App.tsx` uses RELATIVE imports.

---

## Backend contracts (verified 2026-05-30 — do NOT modify)

Branch-scoped; camelCase wire; money in **minor units** as a nested object `price: { currencyCode, minorUnits }`. `organizationId` required in every create/update **body**; `branchId` from the route.

| Method | Route | Permission | Body | Returns |
|---|---|---|---|---|
| GET | `/api/branches/{branchId}/pos/catalog` | `inventory.view` | — | `PosProductDto[]` |
| POST | `/api/branches/{branchId}/pos/categories` | `pos.catalog.manage` | `CreateProductCategoryRequest` | `PosProductCategoryDto` |
| POST | `/api/branches/{branchId}/pos/products` | `pos.catalog.manage` | `CreateProductRequest` | `PosProductDto` |
| PATCH | `/api/branches/{branchId}/pos/products/{productId}` | `pos.catalog.manage` | `UpdateProductRequest` | `PosProductDto` |

`PosProductDto` (camelCase): `productId, organizationId, branchId, categoryId, name, sku, price:{currencyCode,minorUnits}, trackStock, allowNegativeStock, isActive, stockOnHand, createdAtUtc`. **`PosProductDto` carries `categoryId` only — NO category name.**

**Backend gaps (honest limitations, NOT bugs to fix here):** no category list endpoint, no category rename/delete, no product delete. → Categories are derived (id + session-known name, else a short-id label) and create-only; product "removal" is deactivation via `isActive`.

---

## File Structure

- `src/api/types.ts` — add `MoneyMinor`, `PosProduct`, `PosProductCategory`, `CreateProductCategoryRequest`, `CreateProductRequest`, `UpdateProductRequest`. (Task 2)
- `src/api/clubApi.ts` — add `getCatalog`, `createProductCategory`, `createProduct`, `updateProduct`. (Task 2)
- `src/club/monetization/catalog/catalogModel.ts` — pure mapping + category derivation + request builders. (Task 3)
- `src/club/monetization/catalog/useCatalog.ts` — load products → rows + retry. (Task 4)
- `src/club/monetization/catalog/CategoryCreateDialog.tsx` — create a category. (Task 5)
- `src/club/monetization/catalog/ProductFormDialog.tsx` — create/edit a product. (Task 6)
- `src/club/monetization/catalog/CatalogTab.tsx` — list + triggers + read-only gating. (Task 7)
- `src/club/monetization/MonetizationScreen.tsx` + `src/App.tsx` — render the tab, thread `canManageCatalog`. (Task 8)
- `src/i18n/messages.ts` — `products.*` keys. (Task 1)

Colocated `*.test.ts(x)` for the model, hook, both dialogs, the tab, the wrappers, and the updated screen.

---

## Task 1: i18n keys

**Files:** Modify `src/i18n/messages.ts` (both `ru` and `en`); Test `src/i18n/messages.test.ts`.

- [ ] **Step 1: Add the failing test** — append to `src/i18n/messages.test.ts`:

```ts
it('includes the products (catalog) keys', () => {
  for (const key of [
    'products.create', 'products.create.title', 'products.create.submit',
    'products.edit.title', 'products.edit.submit', 'products.empty',
    'products.createCategory', 'products.createCategory.title', 'products.createCategory.submit',
    'products.categoryNote', 'products.categoryUnknown',
    'products.col.category', 'products.col.name', 'products.col.sku', 'products.col.price', 'products.col.stock', 'products.col.status',
    'products.field.category', 'products.field.categoryName', 'products.field.name', 'products.field.sku',
    'products.field.price', 'products.field.currency', 'products.field.trackStock', 'products.field.allowNegativeStock', 'products.field.active',
    'products.status.active', 'products.status.inactive'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
  }
});
```

- [ ] **Step 2: Run `npm test -- messages`** → expect FAIL.

- [ ] **Step 3: Add the keys.** In the `ru` object:

```ts
    'products.create': 'Создать товар',
    'products.create.title': 'Новый товар',
    'products.create.submit': 'Создать',
    'products.edit.title': 'Редактировать товар',
    'products.edit.submit': 'Сохранить',
    'products.empty': 'Товары ещё не созданы.',
    'products.createCategory': 'Создать категорию',
    'products.createCategory.title': 'Новая категория',
    'products.createCategory.submit': 'Создать',
    'products.categoryNote': 'Категории можно только создавать — переименование и удаление недоступны (нет эндпоинта на бэкенде).',
    'products.categoryUnknown': 'Категория',
    'products.col.category': 'Категория',
    'products.col.name': 'Название',
    'products.col.sku': 'Артикул',
    'products.col.price': 'Цена',
    'products.col.stock': 'Остаток',
    'products.col.status': 'Статус',
    'products.field.category': 'Категория',
    'products.field.categoryName': 'Название категории',
    'products.field.name': 'Название',
    'products.field.sku': 'Артикул',
    'products.field.price': 'Цена',
    'products.field.currency': 'Валюта',
    'products.field.trackStock': 'Учитывать остаток',
    'products.field.allowNegativeStock': 'Разрешить отрицательный остаток',
    'products.field.active': 'Активен',
    'products.status.active': 'Активен',
    'products.status.inactive': 'Скрыт',
```

In the `en` object (same keys):

```ts
    'products.create': 'Create product',
    'products.create.title': 'New product',
    'products.create.submit': 'Create',
    'products.edit.title': 'Edit product',
    'products.edit.submit': 'Save',
    'products.empty': 'No products yet.',
    'products.createCategory': 'Create category',
    'products.createCategory.title': 'New category',
    'products.createCategory.submit': 'Create',
    'products.categoryNote': 'Categories can only be created — renaming and deletion are unavailable (no backend endpoint).',
    'products.categoryUnknown': 'Category',
    'products.col.category': 'Category',
    'products.col.name': 'Name',
    'products.col.sku': 'SKU',
    'products.col.price': 'Price',
    'products.col.stock': 'Stock',
    'products.col.status': 'Status',
    'products.field.category': 'Category',
    'products.field.categoryName': 'Category name',
    'products.field.name': 'Name',
    'products.field.sku': 'SKU',
    'products.field.price': 'Price',
    'products.field.currency': 'Currency',
    'products.field.trackStock': 'Track stock',
    'products.field.allowNegativeStock': 'Allow negative stock',
    'products.field.active': 'Active',
    'products.status.active': 'Active',
    'products.status.inactive': 'Hidden',
```

- [ ] **Step 4: Run `npm test -- messages`** → expect PASS.

- [ ] **Step 5: Commit**

```bash
git add src/i18n/messages.ts src/i18n/messages.test.ts
git commit -m "feat(club): add i18n keys for the catalog (products) tab"
```

---

## Task 2: Catalog types + clubApi wrappers

**Files:** Modify `src/api/types.ts`, `src/api/clubApi.ts`; Test `src/api/clubApi.catalog.test.ts`.

- [ ] **Step 1: Write the failing test** — create `src/api/clubApi.catalog.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { ClubApiClient } from './clubApi';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}
function makeClient(fetchImpl: typeof fetch): ClubApiClient {
  return new ClubApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
}

it('getCatalog GETs the branch catalog route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse([])) as unknown as typeof fetch;
  await makeClient(fetchImpl).getCatalog('b1');
  expect(fetchImpl).toHaveBeenCalledWith('https://api.test/api/branches/b1/pos/catalog', expect.objectContaining({ method: 'GET' }));
});

it('createProductCategory POSTs to the categories route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ categoryId: 'c1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createProductCategory('b1', { organizationId: 'org', name: 'Drinks', idempotencyKey: 'k1' });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/pos/categories');
  expect(call[1].method).toBe('POST');
  expect(JSON.parse(call[1].body as string)).toEqual({ organizationId: 'org', name: 'Drinks', idempotencyKey: 'k1' });
});

it('createProduct POSTs to the products route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ productId: 'p1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createProduct('b1', {
    organizationId: 'org', categoryId: 'c1', name: 'Cola', sku: 'SKU1',
    price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: false, allowNegativeStock: false, idempotencyKey: 'k2'
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/pos/products');
  expect(call[1].method).toBe('POST');
});

it('updateProduct PATCHes the product route', async () => {
  const fetchImpl = vi.fn(async () => jsonResponse({ productId: 'p1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).updateProduct('b1', 'p1', {
    organizationId: 'org', categoryId: 'c1', name: 'Cola', sku: 'SKU1',
    price: { currencyCode: 'RUB', minorUnits: 200 }, trackStock: false, allowNegativeStock: false, isActive: true
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/pos/products/p1');
  expect(call[1].method).toBe('PATCH');
});
```

- [ ] **Step 2: Run `npm test -- clubApi.catalog`** → expect FAIL.

- [ ] **Step 3a: Append the types to `src/api/types.ts`:**

```ts
export interface MoneyMinor {
  currencyCode: string;
  minorUnits: number;
}

export interface PosProduct {
  productId: string;
  organizationId: string;
  branchId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: MoneyMinor;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
  stockOnHand: number;
  createdAtUtc: string;
}

export interface PosProductCategory {
  categoryId: string;
  organizationId: string;
  branchId: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateProductCategoryRequest {
  organizationId: string;
  name: string;
  idempotencyKey: string;
}

export interface CreateProductRequest {
  organizationId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: MoneyMinor;
  trackStock: boolean;
  allowNegativeStock: boolean;
  idempotencyKey: string;
}

export interface UpdateProductRequest {
  organizationId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: MoneyMinor;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
}
```

(Note: do NOT reuse the existing `Money` interface — that one is `{ amount, currencyCode }` in major units; POS price is minor units, hence the distinct `MoneyMinor`.)

- [ ] **Step 3b: Add the wrappers to `src/api/clubApi.ts`.** Extend the `import type { ... } from './types';` block (keep it alphabetized) to add: `CreateProductCategoryRequest, CreateProductRequest, PosProduct, PosProductCategory, UpdateProductRequest`. Then add these methods after the tariff wrappers, before the private `send`:

```ts
  public getCatalog(branchId: string): Promise<PosProduct[]> {
    return this.send<PosProduct[]>('GET', `/api/branches/${encodeURIComponent(branchId)}/pos/catalog`);
  }

  public createProductCategory(branchId: string, request: CreateProductCategoryRequest): Promise<PosProductCategory> {
    return this.send<PosProductCategory>('POST', `/api/branches/${encodeURIComponent(branchId)}/pos/categories`, request);
  }

  public createProduct(branchId: string, request: CreateProductRequest): Promise<PosProduct> {
    return this.send<PosProduct>('POST', `/api/branches/${encodeURIComponent(branchId)}/pos/products`, request);
  }

  public updateProduct(branchId: string, productId: string, request: UpdateProductRequest): Promise<PosProduct> {
    return this.send<PosProduct>(
      'PATCH',
      `/api/branches/${encodeURIComponent(branchId)}/pos/products/${encodeURIComponent(productId)}`,
      request
    );
  }
```

- [ ] **Step 4: Run `npm test -- clubApi.catalog`** → expect PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/api/types.ts src/api/clubApi.ts src/api/clubApi.catalog.test.ts
git commit -m "feat(club): add catalog types and clubApi wrappers"
```

---

## Task 3: catalogModel (pure)

**Files:** Create `src/club/monetization/catalog/catalogModel.ts`; Test `src/club/monetization/catalog/catalogModel.test.ts`.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/catalog/catalogModel.test.ts`:

```ts
import { it, expect } from 'vitest';
import type { PosProduct } from '@/api/types';
import {
  toProductRows, deriveCategories, buildCreateCategoryRequest, buildCreateProductRequest,
  buildUpdateProductRequest, type ProductFormValues
} from './catalogModel';

const product: PosProduct = {
  productId: 'p1', organizationId: 'org', branchId: 'b1', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
  price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: true, allowNegativeStock: false,
  isActive: true, stockOnHand: 10, createdAtUtc: '2026-01-01T00:00:00.000Z'
};

const form: ProductFormValues = {
  categoryId: 'c1', name: '  Кола  ', sku: '  SKU1 ', price: 2, currencyCode: 'RUB', trackStock: true, allowNegativeStock: false
};

it('maps products to rows with price in major units', () => {
  const rows = toProductRows([product]);
  expect(rows[0]).toMatchObject({ productId: 'p1', categoryId: 'c1', name: 'Кола', sku: 'SKU1', price: 1.5, currencyCode: 'RUB', stockOnHand: 10, isActive: true });
});

it('derives categories from product ids + session names, labelling unknown ids', () => {
  const cats = deriveCategories(['c1', 'c2', 'c1'], [{ categoryId: 'c1', name: 'Напитки' }], 'Категория');
  expect(cats).toEqual([
    { categoryId: 'c1', name: 'Напитки' },
    { categoryId: 'c2', name: 'Категория c2' }
  ]);
});

it('builds a create-category request, trimming the name', () => {
  expect(buildCreateCategoryRequest('org', '  Снеки ', 'idem')).toEqual({ organizationId: 'org', name: 'Снеки', idempotencyKey: 'idem' });
});

it('builds a create-product request converting price to a minor-unit Money object', () => {
  expect(buildCreateProductRequest('org', form, 'idem2')).toEqual({
    organizationId: 'org', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
    price: { currencyCode: 'RUB', minorUnits: 200 }, trackStock: true, allowNegativeStock: false, idempotencyKey: 'idem2'
  });
});

it('builds an update-product request with isActive', () => {
  expect(buildUpdateProductRequest('org', form, false)).toEqual({
    organizationId: 'org', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
    price: { currencyCode: 'RUB', minorUnits: 200 }, trackStock: true, allowNegativeStock: false, isActive: false
  });
});
```

- [ ] **Step 2: Run `npm test -- catalogModel`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/catalog/catalogModel.ts`:

```ts
import type {
  CreateProductCategoryRequest, CreateProductRequest, PosProduct, UpdateProductRequest
} from '@/api/types';
import { majorToMinor, minorToMajor } from '../../money';

export interface ProductRow {
  productId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: number; // major units, for display
  currencyCode: string;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
  stockOnHand: number;
}

export interface CategoryOption {
  categoryId: string;
  name: string;
}

export interface ProductFormValues {
  categoryId: string;
  name: string;
  sku: string;
  price: number; // major units, as entered
  currencyCode: string;
  trackStock: boolean;
  allowNegativeStock: boolean;
}

export function toProductRows(products: PosProduct[]): ProductRow[] {
  return products.map(p => ({
    productId: p.productId,
    categoryId: p.categoryId,
    name: p.name,
    sku: p.sku,
    price: minorToMajor(p.price.minorUnits),
    currencyCode: p.price.currencyCode,
    trackStock: p.trackStock,
    allowNegativeStock: p.allowNegativeStock,
    isActive: p.isActive,
    stockOnHand: p.stockOnHand
  }));
}

/** The backend has no category list/name endpoint, so the selectable categories
 * are the distinct ids present in the catalog (labelled by a short id when their
 * name is unknown) merged with categories created in this session (which DO carry
 * a name). Session names win. */
export function deriveCategories(categoryIds: string[], sessionCategories: CategoryOption[], unknownLabel: string): CategoryOption[] {
  const byId = new Map<string, string>();
  for (const c of sessionCategories) byId.set(c.categoryId, c.name);
  for (const id of categoryIds) {
    if (!byId.has(id)) byId.set(id, `${unknownLabel} ${id.slice(0, 8)}`);
  }
  return Array.from(byId.entries()).map(([categoryId, name]) => ({ categoryId, name }));
}

export function buildCreateCategoryRequest(organizationId: string, name: string, idempotencyKey: string): CreateProductCategoryRequest {
  return { organizationId, name: name.trim(), idempotencyKey };
}

export function buildCreateProductRequest(organizationId: string, form: ProductFormValues, idempotencyKey: string): CreateProductRequest {
  return {
    organizationId,
    categoryId: form.categoryId,
    name: form.name.trim(),
    sku: form.sku.trim(),
    price: { currencyCode: form.currencyCode, minorUnits: majorToMinor(form.price) },
    trackStock: form.trackStock,
    allowNegativeStock: form.allowNegativeStock,
    idempotencyKey
  };
}

export function buildUpdateProductRequest(organizationId: string, form: ProductFormValues, isActive: boolean): UpdateProductRequest {
  return {
    organizationId,
    categoryId: form.categoryId,
    name: form.name.trim(),
    sku: form.sku.trim(),
    price: { currencyCode: form.currencyCode, minorUnits: majorToMinor(form.price) },
    trackStock: form.trackStock,
    allowNegativeStock: form.allowNegativeStock,
    isActive
  };
}
```

- [ ] **Step 4: Run `npm test -- catalogModel`** → expect PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/catalog/catalogModel.ts src/club/monetization/catalog/catalogModel.test.ts
git commit -m "feat(club): add catalog model (rows, category derivation, request builders)"
```

---

## Task 4: useCatalog hook

**Files:** Create `src/club/monetization/catalog/useCatalog.ts`; Test `src/club/monetization/catalog/useCatalog.test.ts`.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/catalog/useCatalog.test.ts`:

```ts
import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { PosProduct } from '@/api/types';
import { useCatalog } from './useCatalog';

const product: PosProduct = {
  productId: 'p1', organizationId: 'org', branchId: 'b1', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
  price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: false, allowNegativeStock: false,
  isActive: true, stockOnHand: 10, createdAtUtc: '2026-01-01T00:00:00.000Z'
};

it('loads the catalog into product rows', async () => {
  const client = { getCatalog: vi.fn(async () => [product]) };
  const { result } = renderHook(() => useCatalog(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Кола']);
  expect(result.current.rows[0].price).toBe(1.5);
});

it('reports an error when the load fails', async () => {
  const client = { getCatalog: vi.fn(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useCatalog(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run `npm test -- useCatalog`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/catalog/useCatalog.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toProductRows, type ProductRow } from './catalogModel';

type Loadable = Pick<ClubApiClient, 'getCatalog'>;

export type CatalogState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: ProductRow[]; retry: () => void };

export function useCatalog(client: Loadable, branchId: string): CatalogState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<ProductRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getCatalog(branchId)
      .then(products => { if (!cancelled) { setRows(toProductRows(products)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
```

- [ ] **Step 4: Run `npm test -- useCatalog`** → expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/catalog/useCatalog.ts src/club/monetization/catalog/useCatalog.test.ts
git commit -m "feat(club): add useCatalog hook (load catalog into rows)"
```

---

## Task 5: CategoryCreateDialog

**Files:** Create `src/club/monetization/catalog/CategoryCreateDialog.tsx`; Test `src/club/monetization/catalog/CategoryCreateDialog.test.tsx`.

`idempotencyKey` is `crypto.randomUUID()`. On success the created category (`{ categoryId, name }`) is reported via `onCreated` so the tab can add it to the session list and select it.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/catalog/CategoryCreateDialog.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CategoryCreateDialog } from './CategoryCreateDialog';

it('creates a category and reports it via onCreated', async () => {
  const onCreated = vi.fn();
  const client = {
    createProductCategory: vi.fn(async () => ({
      categoryId: 'c9', organizationId: 'org', branchId: 'b1', name: 'Снеки', isActive: true, createdAtUtc: '2026-01-01T00:00:00.000Z'
    }))
  };
  render(
    <I18nProvider><ToastProvider>
      <CategoryCreateDialog open branchId="b1" organizationId="org" client={client as never} onCreated={onCreated} onOpenChange={() => {}} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.change(screen.getByLabelText('Название категории'), { target: { value: 'Снеки' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(client.createProductCategory).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org', name: 'Снеки' })));
  await waitFor(() => expect(onCreated).toHaveBeenCalledWith({ categoryId: 'c9', name: 'Снеки' }));
});
```

- [ ] **Step 2: Run `npm test -- CategoryCreateDialog`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/catalog/CategoryCreateDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { buildCreateCategoryRequest, type CategoryOption } from './catalogModel';

type Actions = Pick<ClubApiClient, 'createProductCategory'>;

export function CategoryCreateDialog({ open, branchId, organizationId, client, onCreated, onOpenChange }: {
  open: boolean;
  branchId: string;
  organizationId: string;
  client: Actions;
  onCreated: (category: CategoryOption) => void;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [name, setName] = useState('');
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    try {
      const created = await client.createProductCategory(branchId, buildCreateCategoryRequest(organizationId, name, crypto.randomUUID()));
      onCreated({ categoryId: created.categoryId, name: created.name });
      toast({ title: t('toast.saved'), variant: 'success' });
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{t('products.createCategory.title')}</DialogTitle>
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('products.field.categoryName')}</span>
          <Input aria-label={t('products.field.categoryName')} value={name} onChange={e => setName(e.target.value)} />
        </label>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || name.trim() === ''} onClick={() => void submit()}>{t('products.createCategory.submit')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 4: Run `npm test -- CategoryCreateDialog`** → expect PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/catalog/CategoryCreateDialog.tsx src/club/monetization/catalog/CategoryCreateDialog.test.tsx
git commit -m "feat(club): add CategoryCreateDialog"
```

---

## Task 6: ProductFormDialog (create + edit)

**Files:** Create `src/club/monetization/catalog/ProductFormDialog.tsx`; Test `src/club/monetization/catalog/ProductFormDialog.test.tsx`.

The category is chosen via the Radix `Select` primitive (as in `DeviceDrawer`). The default selection is the initial product's category (edit) or the first available category (create), so the tests do not need to open the Radix dropdown in jsdom. `crypto.randomUUID()` for the idempotency key.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/catalog/ProductFormDialog.test.tsx`:

```tsx
import type { ComponentProps } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { CategoryOption, ProductRow } from './catalogModel';
import { ProductFormDialog } from './ProductFormDialog';

type DialogProps = ComponentProps<typeof ProductFormDialog>;

const categories: CategoryOption[] = [{ categoryId: 'c1', name: 'Напитки' }];

function client(overrides: Record<string, unknown> = {}) {
  return {
    createProduct: vi.fn(async () => ({ productId: 'p1' })),
    updateProduct: vi.fn(async () => ({ productId: 'p1' })),
    ...overrides
  };
}

function renderDialog(props: Record<string, unknown>) {
  const merged = {
    open: true, branchId: 'b1', organizationId: 'org', categories,
    onOpenChange: () => {}, onDone: () => {},
    ...props
  } as unknown as DialogProps;
  render(<I18nProvider><ToastProvider><ProductFormDialog {...merged} /></ToastProvider></I18nProvider>);
}

it('creates a product with the default category and minor-unit price', async () => {
  const c = client();
  renderDialog({ mode: 'create', client: c });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Кола' } });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '1.5' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(c.createProduct).toHaveBeenCalledWith('b1', expect.objectContaining({
    organizationId: 'org', categoryId: 'c1', name: 'Кола', price: { currencyCode: 'RUB', minorUnits: 150 }
  })));
});

it('updates a product in edit mode', async () => {
  const c = client();
  const initial: ProductRow = {
    productId: 'p1', categoryId: 'c1', name: 'Кола', sku: 'SKU1', price: 1.5, currencyCode: 'RUB',
    trackStock: false, allowNegativeStock: false, isActive: true, stockOnHand: 10
  };
  renderDialog({ mode: 'edit', client: c, initial });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '2' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(c.updateProduct).toHaveBeenCalledWith('b1', 'p1', expect.objectContaining({
    categoryId: 'c1', name: 'Кола', price: { currencyCode: 'RUB', minorUnits: 200 }, isActive: true
  })));
});
```

- [ ] **Step 2: Run `npm test -- ProductFormDialog`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/catalog/ProductFormDialog.tsx`:

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import {
  buildCreateProductRequest, buildUpdateProductRequest, type CategoryOption, type ProductFormValues, type ProductRow
} from './catalogModel';

type Actions = Pick<ClubApiClient, 'createProduct' | 'updateProduct'>;

export function ProductFormDialog({ open, mode, branchId, organizationId, client, categories, initial, onOpenChange, onDone }: {
  open: boolean;
  mode: 'create' | 'edit';
  branchId: string;
  organizationId: string;
  client: Actions;
  categories: CategoryOption[];
  initial?: ProductRow;
  onOpenChange: (open: boolean) => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [categoryId, setCategoryId] = useState(initial?.categoryId ?? categories[0]?.categoryId ?? '');
  const [name, setName] = useState(initial?.name ?? '');
  const [sku, setSku] = useState(initial?.sku ?? '');
  const [price, setPrice] = useState(String(initial?.price ?? '0'));
  const [currency, setCurrency] = useState(initial?.currencyCode ?? 'RUB');
  const [trackStock, setTrackStock] = useState(initial?.trackStock ?? false);
  const [allowNegativeStock, setAllowNegativeStock] = useState(initial?.allowNegativeStock ?? false);
  const [active, setActive] = useState(initial?.isActive ?? true);
  const [pending, setPending] = useState(false);

  const valid = categoryId !== '' && name.trim() !== '' && currency.trim() !== '' && Number(price) >= 0;

  function formValues(): ProductFormValues {
    return { categoryId, name, sku, price: Number(price), currencyCode: currency.trim(), trackStock, allowNegativeStock };
  }

  async function submit() {
    setPending(true);
    try {
      if (mode === 'create') {
        await client.createProduct(branchId, buildCreateProductRequest(organizationId, formValues(), crypto.randomUUID()));
      } else if (initial !== undefined) {
        await client.updateProduct(branchId, initial.productId, buildUpdateProductRequest(organizationId, formValues(), active));
      }
      toast({ title: t('toast.saved'), variant: 'success' });
      onDone();
      onOpenChange(false);
    } catch {
      toast({ title: t('toast.failed'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{mode === 'create' ? t('products.create.title') : t('products.edit.title')}</DialogTitle>
        <div className="flex flex-col gap-3">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.category')}</span>
            <Select value={categoryId} onValueChange={setCategoryId}>
              <SelectTrigger aria-label={t('products.field.category')}><SelectValue placeholder={t('products.field.category')} /></SelectTrigger>
              <SelectContent>
                {categories.map(c => <SelectItem key={c.categoryId} value={c.categoryId}>{c.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.name')}</span>
            <Input aria-label={t('products.field.name')} value={name} onChange={e => setName(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.sku')}</span>
            <Input aria-label={t('products.field.sku')} value={sku} onChange={e => setSku(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.price')}</span>
            <Input aria-label={t('products.field.price')} value={price} onChange={e => setPrice(e.target.value)} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('products.field.currency')}</span>
            <Input aria-label={t('products.field.currency')} value={currency} onChange={e => setCurrency(e.target.value)} />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={trackStock} aria-label={t('products.field.trackStock')} onCheckedChange={c => setTrackStock(c === true)} />
            {t('products.field.trackStock')}
          </label>
          <label className="flex items-center gap-2 text-sm">
            <Checkbox checked={allowNegativeStock} aria-label={t('products.field.allowNegativeStock')} onCheckedChange={c => setAllowNegativeStock(c === true)} />
            {t('products.field.allowNegativeStock')}
          </label>
          {mode === 'edit' && (
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={active} aria-label={t('products.field.active')} onCheckedChange={setActive} />
              {t('products.field.active')}
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={() => void submit()}>
            {mode === 'create' ? t('products.create.submit') : t('products.edit.submit')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

Note on primitives: reuses `Checkbox` (`onCheckedChange(c)` where `c` is `boolean | 'indeterminate'`), `Switch` (`onCheckedChange(boolean)`), and `Select` (`value`/`onValueChange`) exactly as `CreateOperatorDialog`/`DeviceDrawer` use them. If any signature differs, adapt the handler (not the test).

- [ ] **Step 4: Run `npm test -- ProductFormDialog`** → expect PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/catalog/ProductFormDialog.tsx src/club/monetization/catalog/ProductFormDialog.test.tsx
git commit -m "feat(club): add ProductFormDialog (create + edit product)"
```

---

## Task 7: CatalogTab

**Files:** Create `src/club/monetization/catalog/CatalogTab.tsx`; Test `src/club/monetization/catalog/CatalogTab.test.tsx`.

- [ ] **Step 1: Write the failing test** — create `src/club/monetization/catalog/CatalogTab.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PosProduct } from '@/api/types';
import { CatalogTab } from './CatalogTab';

const product: PosProduct = {
  productId: 'p1', organizationId: 'org', branchId: 'b1', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
  price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: false, allowNegativeStock: false,
  isActive: true, stockOnHand: 10, createdAtUtc: '2026-01-01T00:00:00.000Z'
};

function fakeClient() {
  return {
    getCatalog: vi.fn(async () => [product]),
    createProductCategory: vi.fn(async () => ({ categoryId: 'c9', organizationId: 'org', branchId: 'b1', name: 'Снеки', isActive: true, createdAtUtc: '' })),
    createProduct: vi.fn(async () => ({ productId: 'p2' })),
    updateProduct: vi.fn(async () => ({ productId: 'p1' }))
  };
}

function renderTab(canManage: boolean) {
  render(
    <I18nProvider><ToastProvider>
      <CatalogTab client={fakeClient() as never} branchId="b1" organizationId="org" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
}

it('renders product rows', async () => {
  renderTab(true);
  expect(await screen.findByText('Кола')).toBeInTheDocument();
});

it('opens the create-product dialog when managing', async () => {
  renderTab(true);
  await screen.findByText('Кола');
  fireEvent.click(screen.getByRole('button', { name: 'Создать товар' }));
  expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
});

it('hides the create triggers when read-only', async () => {
  renderTab(false);
  await screen.findByText('Кола');
  expect(screen.queryByRole('button', { name: 'Создать товар' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Создать категорию' })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- CatalogTab`** → expect FAIL.

- [ ] **Step 3: Write the implementation** — create `src/club/monetization/catalog/CatalogTab.tsx`:

```tsx
import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useCatalog } from './useCatalog';
import { CategoryCreateDialog } from './CategoryCreateDialog';
import { ProductFormDialog } from './ProductFormDialog';
import { deriveCategories, type CategoryOption, type ProductRow } from './catalogModel';

type Client = Pick<ClubApiClient, 'getCatalog' | 'createProductCategory' | 'createProduct' | 'updateProduct'>;

export function CatalogTab({ client, branchId, organizationId, canManage }: {
  client: Client;
  branchId: string;
  organizationId: string;
  canManage: boolean;
}) {
  const { t, formatNumber, formatCurrency } = useI18n();
  const state = useCatalog(client, branchId);
  const [sessionCategories, setSessionCategories] = useState<CategoryOption[]>([]);
  const [creatingCategory, setCreatingCategory] = useState(false);
  const [creatingProduct, setCreatingProduct] = useState(false);
  const [editing, setEditing] = useState<ProductRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { rows, retry } = state;
  const categories = deriveCategories(rows.map(r => r.categoryId), sessionCategories, t('products.categoryUnknown'));
  const categoryName = (id: string) => categories.find(c => c.categoryId === id)?.name ?? id;

  function onCategoryCreated(category: CategoryOption) {
    setSessionCategories(prev => [...prev.filter(c => c.categoryId !== category.categoryId), category]);
    retry();
  }

  return (
    <div className="flex flex-col gap-4">
      {canManage && (
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={() => setCreatingCategory(true)}>{t('products.createCategory')}</Button>
          <Button onClick={() => setCreatingProduct(true)}>{t('products.create')}</Button>
        </div>
      )}

      {rows.length === 0 ? (
        <EmptyState message={t('products.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('products.col.category')}</TableHead>
              <TableHead>{t('products.col.name')}</TableHead>
              <TableHead>{t('products.col.sku')}</TableHead>
              <TableHead>{t('products.col.price')}</TableHead>
              <TableHead>{t('products.col.stock')}</TableHead>
              <TableHead>{t('products.col.status')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(row => (
              <TableRow key={row.productId} data-clickable={canManage ? 'true' : undefined}
                onClick={canManage ? () => setEditing(row) : undefined}>
                <TableCell className="text-sm text-muted-foreground">{categoryName(row.categoryId)}</TableCell>
                <TableCell className="font-medium">{row.name}</TableCell>
                <TableCell className="text-sm">{row.sku}</TableCell>
                <TableCell className="tabular-nums">{formatCurrency(row.price, row.currencyCode)}</TableCell>
                <TableCell className="tabular-nums">{row.trackStock ? formatNumber(row.stockOnHand) : '—'}</TableCell>
                <TableCell>
                  <Badge variant={row.isActive ? 'default' : 'secondary'}>
                    {row.isActive ? t('products.status.active') : t('products.status.inactive')}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('products.categoryNote')}</p>

      {creatingCategory && (
        <CategoryCreateDialog
          open branchId={branchId} organizationId={organizationId} client={client}
          onCreated={onCategoryCreated}
          onOpenChange={o => { if (!o) setCreatingCategory(false); }}
        />
      )}
      {creatingProduct && (
        <ProductFormDialog
          open mode="create" branchId={branchId} organizationId={organizationId} client={client} categories={categories}
          onOpenChange={o => { if (!o) setCreatingProduct(false); }}
          onDone={() => retry()}
        />
      )}
      {editing !== null && (
        <ProductFormDialog
          key={editing.productId}
          open mode="edit" branchId={branchId} organizationId={organizationId} client={client} categories={categories} initial={editing}
          onOpenChange={o => { if (!o) setEditing(null); }}
          onDone={() => retry()}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run `npm test -- CatalogTab`** → expect PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/club/monetization/catalog/CatalogTab.tsx src/club/monetization/catalog/CatalogTab.test.tsx
git commit -m "feat(club): add CatalogTab (products list, category/product create, edit)"
```

---

## Task 8: Wire CatalogTab into MonetizationScreen + thread canManageCatalog

**Files:** Modify `src/club/monetization/MonetizationScreen.tsx`, `src/club/monetization/MonetizationScreen.test.tsx`, `src/App.tsx`.

- [ ] **Step 1: Update the MonetizationScreen test.** In `src/club/monetization/MonetizationScreen.test.tsx`: (a) add `getCatalog: vi.fn(async () => [])` to the fake client; (b) add `canManageCatalog` to the rendered props; (c) replace the "placeholder on the products tab" test with one asserting the catalog renders. Concretely, update the `setup` client + render to:

```tsx
  const client = { getTariffOptions: vi.fn(async () => [option]), getCatalog: vi.fn(async () => []) };
  render(
    <I18nProvider><ToastProvider>
      <MonetizationScreen client={client as never} branchId="b1" organizationId="org" canManageTariffs canManageCatalog />
    </ToastProvider></I18nProvider>
  );
```

and replace the second test with:

```tsx
it('shows the catalog on the products tab', async () => {
  setup();
  await screen.findByText('Дневной');
  const tab = screen.getByRole('tab', { name: 'Товары' });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  expect(await screen.findByText('Товары ещё не созданы.')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run `npm test -- MonetizationScreen`** → expect FAIL (prop `canManageCatalog` missing / products tab still placeholder).

- [ ] **Step 3: Update MonetizationScreen.** In `src/club/monetization/MonetizationScreen.tsx`:
- add the import: `import { CatalogTab } from './catalog/CatalogTab';`
- change the signature to add `canManageCatalog: boolean`:
  ```tsx
  export function MonetizationScreen({ client, branchId, organizationId, canManageTariffs, canManageCatalog }: {
    client: ClubApiClient;
    branchId: string;
    organizationId: string;
    canManageTariffs: boolean;
    canManageCatalog: boolean;
  }) {
  ```
- replace the products `TabsContent` body:
  ```tsx
  <TabsContent value="products">
    <p className="text-sm text-muted-foreground">{t('monetization.soon')}</p>
  </TabsContent>
  ```
  with:
  ```tsx
  <TabsContent value="products">
    <CatalogTab client={client} branchId={branchId} organizationId={organizationId} canManage={canManageCatalog} />
  </TabsContent>
  ```

- [ ] **Step 4: Thread the prop from App.tsx.** In `src/App.tsx`, the `clubMonetization` render branch, add `canManageCatalog`:

```tsx
          <MonetizationScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            canManageTariffs={session.permissions.includes('tariffs.manage')}
            canManageCatalog={session.permissions.includes('pos.catalog.manage')}
          />
```

- [ ] **Step 5: Run `npm test -- MonetizationScreen`** → expect PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/club/monetization/MonetizationScreen.tsx src/club/monetization/MonetizationScreen.test.tsx src/App.tsx
git commit -m "feat(club): render the catalog tab + thread pos.catalog.manage"
```

---

## Task 9: Full suite + build gate

**Files:** none (verification only).

- [ ] **Step 1: Run `npm test`** → all pass (this plan adds ~7 files + ~17 tests over the 5a baseline of 60 files / 194 tests).

- [ ] **Step 2: Run `npm run build`** → clean `tsc -b && vite build`. (Remember: `tsc -b` is the real type check — vitest/esbuild does NOT type-check. If a `*.test.tsx` render helper spreads an untyped object into a component, type it via `ComponentProps<typeof X>` as the ProductFormDialog test already does.)

- [ ] **Step 3: Commit (only if anything is uncommitted)**

```bash
git add -A
git commit -m "chore(club): catalog tab green on suite and build"
```

If nothing is uncommitted, skip.

---

## Self-Review

**Spec coverage** (monetization design spec, Товары section):
- List products via `getCatalog`, grouped/shown by category → Tasks 3/4/7 (category shown as a resolved column). ✓
- Create category (create-only) + create product → Tasks 5/6/7. ✓
- Edit product incl. deactivate via `isActive` → Task 6 (Switch in edit mode). ✓
- Category limitation handled honestly (derived id+session-name, create-only note) → Tasks 3/7 (`deriveCategories`, `products.categoryNote`). ✓
- Money in minor units via shared helper → Tasks 2/3 (`MoneyMinor`, `majorToMinor`). ✓
- No new backend contracts; new wrappers + camelCase types → Task 2. ✓
- Role gating via `pos.catalog.manage` → Tasks 7/8; read-only hides triggers + row-edit → Task 7. ✓
- Data-region states → Tasks 4/7. ✓
- Plugged into the Товары tab → Task 8. ✓

**Deliberate choices (documented):**
- **Flat table with a Категория column** instead of visually grouping rows by category — same information, simpler/testable; grouping is a future polish.
- **Deactivation via the `Active` Switch in the edit dialog** (no product DELETE on the backend), consistent with 5a's tariff approach — no separate ConfirmDialog.
- **Category picker uses the real Radix `Select`** but the default selection makes the create/edit tests deterministic without opening the dropdown.

**Placeholder scan:** no TBD/"handle edge cases"; every code step is complete with real code and exact commands.

**Type consistency:** `MoneyMinor`/`PosProduct`/`PosProductCategory` + the three request types (Task 2) are consumed unchanged in Tasks 3/5/6. `ProductRow`/`CategoryOption`/`ProductFormValues` (Task 3) flow into Tasks 4/6/7. `useCatalog(client, branchId): CatalogState` (Task 4) consumed in Task 7. `CategoryCreateDialog` props (Task 5) and `ProductFormDialog` props `{ open, mode, branchId, organizationId, client, categories, initial?, onOpenChange, onDone }` (Task 6) match Task 7's render sites. `CatalogTab` props `{ client, branchId, organizationId, canManage }` (Task 7) match Task 8. `MonetizationScreen` gains `canManageCatalog`, satisfied by Task 8's App.tsx call site. Wrapper names (`getCatalog`/`createProductCategory`/`createProduct`/`updateProduct`) identical across Tasks 2/5/6/7. Routes + `pos.catalog.manage`/`inventory.view` match the verified backend table.

---

## Execution Handoff

Two execution options:

1. **Subagent-Driven (recommended)** — fresh subagent per task, two-stage review between tasks.
2. **Inline Execution** — execute tasks in this session with checkpoints.
