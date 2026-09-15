import { describe, expect, it } from 'bun:test';
import { deriveCategoryOptions } from './categoryModel';

describe('deriveCategoryOptions', () => {
  // Товар несёт только `categoryId` — имени категории в нём нет, поэтому у категории, которой
  // нет в справочнике, остаётся одна заглушка на всех.
  it('deduplicates product and session categories and labels unknown ids', () => {
    expect(deriveCategoryOptions([], [
      { categoryId: 'aaaaaaaa-1111' },
      { categoryId: 'aaaaaaaa-1111' },
      { categoryId: 'bbbbbbbb-2222' }
    ], [{ categoryId: 'cccccccc-3333', label: 'Снеки', isActive: true }], 'Категория')).toEqual([
      { categoryId: 'aaaaaaaa-1111', label: 'Категория aaaaaaaa', isActive: true },
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

  // Имя приходит из справочника, и переименование видно сразу, а не после того, как перезапишут
  // каждый товар. Товар добавляет в список только сам факт своей категории.
  it('имя берётся из справочника, а не от товара', () => {
    expect(deriveCategoryOptions(
      [{ categoryId: 'aaaaaaaa-1111', name: 'Снеки', isActive: true }],
      [{ categoryId: 'aaaaaaaa-1111' }],
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
