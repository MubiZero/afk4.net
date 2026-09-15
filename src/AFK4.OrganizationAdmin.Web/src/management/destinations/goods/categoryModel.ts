import { readBoolean, readString } from '../../../operatorHelpers';

export interface CategoryOption {
  categoryId: string;
  label: string;
  /** Скрытая категория не предлагается на стойке и в магазине оболочки вместе со своими товарами. */
  isActive: boolean;
}

/**
 * Список категорий для выбора.
 *
 * Главный источник — сам справочник категорий филиала. Товары остаются запасным: если у товара
 * стоит категория, которой в справочнике нет, скрыть такой товар из выбора хуже, чем показать его
 * категорию под заглушкой. Заведённые в этом же окне идут третьими — на случай, когда справочник
 * ещё не перечитан.
 *
 * Раньше справочника не существовало вовсе, и весь список собирался из товаров: категория без
 * единого товара пропадала после перезагрузки, а имя показывалось как «категория 3f2a91c4».
 */
export function deriveCategoryOptions(
  categories: readonly unknown[],
  products: readonly unknown[],
  sessionCategories: readonly CategoryOption[],
  unknownPrefix: string
): CategoryOption[] {
  const options = new Map<string, CategoryOption>();
  // Справочник приходит уже в своём порядке (sortOrder на сервере), и пересортировка здесь его бы потеряла.
  for (const category of categories) {
    const categoryId = readString(category, 'categoryId');
    if (!categoryId) continue;
    options.set(categoryId, {
      categoryId,
      label: readString(category, 'name') || `${unknownPrefix} ${categoryId.slice(0, 8)}`,
      isActive: readBoolean(category, 'isActive')
    });
  }
  for (const product of products) {
    const categoryId = readString(product, 'categoryId');
    if (!categoryId || options.has(categoryId)) continue;
    // Категория, известная только по товару, считается видимой: скрыть её за отсутствие в
    // справочнике значило бы придумать решение, которого никто не принимал.
    options.set(categoryId, {
      categoryId,
      label: readString(product, 'categoryName') || `${unknownPrefix} ${categoryId.slice(0, 8)}`,
      isActive: true
    });
  }
  for (const category of sessionCategories) {
    if (category.categoryId && !options.has(category.categoryId)) options.set(category.categoryId, category);
  }
  return [...options.values()];
}
