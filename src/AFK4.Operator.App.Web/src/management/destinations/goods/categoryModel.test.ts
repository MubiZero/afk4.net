import { describe, expect, it } from 'bun:test';
import { deriveCategoryOptions } from './categoryModel';

describe('deriveCategoryOptions', () => {
  it('deduplicates product and session categories and labels unknown ids', () => {
    expect(deriveCategoryOptions([
      { categoryId: 'aaaaaaaa-1111', categoryName: 'Напитки' },
      { categoryId: 'aaaaaaaa-1111', categoryName: 'Другое имя' },
      { categoryId: 'bbbbbbbb-2222' }
    ], [{ categoryId: 'cccccccc-3333', label: 'Снеки' }], 'Категория')).toEqual([
      { categoryId: 'aaaaaaaa-1111', label: 'Напитки' },
      { categoryId: 'bbbbbbbb-2222', label: 'Категория bbbbbbbb' },
      { categoryId: 'cccccccc-3333', label: 'Снеки' }
    ]);
  });
});
