import type { PosProductCategoryDto } from './operatorApiClients';

/**
 * Справочник категорий филиала: имя, место в ряду и видимость.
 *
 * Сервер отдаёт у товара только `categoryId`. Экраны, которым нужно имя, читали у товара поле
 * `categoryName`, которого в контракте нет вовсе: стойка показывала кассиру ряд GUID'ов, а на
 * складе колонка категории просто не появлялась. Имя живёт здесь — в одном месте на все экраны.
 */
export type PosCategoryDirectory = ReadonlyMap<string, { name: string; sortOrder: number; isActive: boolean }>;

export function readCategoryDirectory(categories: readonly PosProductCategoryDto[]): PosCategoryDirectory {
  const directory = new Map<string, { name: string; sortOrder: number; isActive: boolean }>();
  for (const category of categories) {
    if (!category.categoryId) continue;
    directory.set(category.categoryId, {
      name: category.name,
      sortOrder: category.sortOrder,
      isActive: category.isActive
    });
  }
  return directory;
}
