import { it, expect } from 'bun:test';
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
