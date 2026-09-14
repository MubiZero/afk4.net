import { describe, expect, it } from 'bun:test';
import { deriveCategoryOptions } from './categoryModel';

describe('deriveCategoryOptions', () => {
  it('deduplicates product and session categories and labels unknown ids', () => {
    expect(deriveCategoryOptions([], [
      { categoryId: 'aaaaaaaa-1111', categoryName: 'Напитки' },
      { categoryId: 'aaaaaaaa-1111', categoryName: 'Другое имя' },
      { categoryId: 'bbbbbbbb-2222' }
    ], [{ categoryId: 'cccccccc-3333', label: 'Снеки' }], 'Категория')).toEqual([
      { categoryId: 'aaaaaaaa-1111', label: 'Напитки' },
      { categoryId: 'bbbbbbbb-2222', label: 'Категория bbbbbbbb' },
      { categoryId: 'cccccccc-3333', label: 'Снеки' }
    ]);
  });

  // Справочник — главный источник. До него категория, к которой ещё не привязан ни один товар,
  // исчезала из выбора при первой же перезагрузке страницы.
  it('показывает категорию без единого товара', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки' }],
      [],
      [],
      'Категория'
    )).toEqual([{ categoryId: 'aaaaaaaa-1111', label: 'Снеки' }]);
  });

  // Имя из справочника сильнее имени, приехавшего с товаром: переименование должно быть видно
  // сразу, а не после того, как перезапишут каждый товар.
  it('имя из справочника перекрывает имя из товара', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки' }],
      [{ categoryId: 'aaaaaaaa-1111', categoryName: 'Старое имя' }],
      [],
      'Категория'
    )).toEqual([{ categoryId: 'aaaaaaaa-1111', label: 'Снеки' }]);
  });

  // Товар с категорией, которой в справочнике нет, скрывать нельзя: пусть с заглушкой, но виден.
  it('не теряет товар с категорией вне справочника', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки' }],
      [{ categoryId: 'bbbbbbbb-2222' }],
      [],
      'Категория'
    )).toEqual([
      { categoryId: 'aaaaaaaa-1111', label: 'Снеки' },
      { categoryId: 'bbbbbbbb-2222', label: 'Категория bbbbbbbb' }
    ]);
  });
});
