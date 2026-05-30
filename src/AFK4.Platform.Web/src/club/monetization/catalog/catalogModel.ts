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
