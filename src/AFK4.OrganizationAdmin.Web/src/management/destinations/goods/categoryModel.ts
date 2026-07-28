import { readString } from '../../../operatorHelpers';

export interface CategoryOption {
  categoryId: string;
  label: string;
}

export function deriveCategoryOptions(
  products: readonly unknown[],
  sessionCategories: readonly CategoryOption[],
  unknownPrefix: string
): CategoryOption[] {
  const options = new Map<string, string>();
  for (const product of products) {
    const categoryId = readString(product, 'categoryId');
    if (!categoryId || options.has(categoryId)) continue;
    options.set(categoryId, readString(product, 'categoryName') || `${unknownPrefix} ${categoryId.slice(0, 8)}`);
  }
  for (const category of sessionCategories) {
    if (category.categoryId && !options.has(category.categoryId)) options.set(category.categoryId, category.label);
  }
  return [...options].map(([categoryId, label]) => ({ categoryId, label }));
}
