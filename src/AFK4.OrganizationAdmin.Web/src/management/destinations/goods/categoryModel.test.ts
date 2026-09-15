import { describe, expect, it } from 'bun:test';
import { deriveCategoryOptions } from './categoryModel';

describe('deriveCategoryOptions', () => {
  it('deduplicates product and session categories and labels unknown ids', () => {
    expect(deriveCategoryOptions([], [
      { categoryId: 'aaaaaaaa-1111', categoryName: 'Напитки' },
      { categoryId: 'aaaaaaaa-1111', categoryName: 'Другое имя' },
      { categoryId: 'bbbbbbbb-2222' }
    ], [{ categoryId: 'cccccccc-3333', label: 'Снеки', isActive: true }], 'Категория')).toEqual([
      { categoryId: 'aaaaaaaa-1111', label: 'Напитки', isActive: true },
      { categoryId: 'bbbbbbbb-2222', label: 'Категория bbbbbbbb', isActive: true },
      { categoryId: 'cccccccc-3333', label: 'Снеки', isActive: true }
    ]);
  });

  // Справочник — главный источник. До него категория, к которой ещё не привязан ни один товар,
  // исчезала из выбора при первой же перезагрузке страницы.
  it('показывает категорию без единого товара', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки', isActive: true }],
      [],
      [],
      'Категория'
    )).toEqual([{ categoryId: 'aaaaaaaa-1111', label: 'Снеки', isActive: true }]);
  });

  // Имя из справочника сильнее имени, приехавшего с товаром: переименование должно быть видно
  // сразу, а не после того, как перезапишут каждый товар.
  it('имя из справочника перекрывает имя из товара', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки', isActive: true }],
      [{ categoryId: 'aaaaaaaa-1111', categoryName: 'Старое имя' }],
      [],
      'Категория'
    )).toEqual([{ categoryId: 'aaaaaaaa-1111', label: 'Снеки', isActive: true }]);
  });

  // Товар с категорией, которой в справочнике нет, скрывать нельзя: пусть с заглушкой, но виден.
  it('не теряет товар с категорией вне справочника', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки', isActive: true }],
      [{ categoryId: 'bbbbbbbb-2222' }],
      [],
      'Категория'
    )).toEqual([
      { categoryId: 'aaaaaaaa-1111', label: 'Снеки', isActive: true },
      { categoryId: 'bbbbbbbb-2222', label: 'Категория bbbbbbbb', isActive: true }
    ]);
  });

  // Порядок справочника — это порядок на стойке. Пересортировка здесь стоила бы владельцу
  // расстановки, которую он сделал руками.
  it('сохраняет порядок справочника, а не алфавит', () => {
    expect(deriveCategoryOptions(
      [
        { categoryId: 'aaaaaaaa-1111', name: 'Напитки', isActive: true },
        { categoryId: 'bbbbbbbb-2222', name: 'Батарейки', isActive: true }
      ],
      [],
      [],
      'Категория'
    ).map((category) => category.label)).toEqual(['Напитки', 'Батарейки']);
  });

  // Скрытая категория из справочника не пропадает: владельцу нужно её видеть, чтобы вернуть.
  it('доносит скрытость категории из справочника', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки', isActive: false }],
      [],
      [],
      'Категория'
    )).toEqual([{ categoryId: 'aaaaaaaa-1111', label: 'Снеки', isActive: false }]);
  });
});
