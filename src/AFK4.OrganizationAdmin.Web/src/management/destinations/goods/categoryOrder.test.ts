import { describe, expect, it } from 'bun:test';
import { moveCategory } from './categoryOrder';

const categories = [
  { categoryId: 'a', label: 'Батарейки', isActive: true },
  { categoryId: 'b', label: 'Напитки', isActive: true },
  { categoryId: 'c', label: 'Снеки', isActive: false }
];

describe('moveCategory', () => {
  it('меняет местами с соседом сверху', () => {
    expect(moveCategory(categories, 'b', -1)).toEqual(['b', 'a', 'c']);
  });

  it('меняет местами с соседом снизу', () => {
    expect(moveCategory(categories, 'b', 1)).toEqual(['a', 'c', 'b']);
  });

  // Скрытая категория участвует в порядке наравне: она вернётся на то же место, когда её покажут.
  it('не пропускает скрытую категорию при перестановке', () => {
    expect(moveCategory(categories, 'c', -1)).toEqual(['a', 'c', 'b']);
  });

  it('с краёв не двигает', () => {
    expect(moveCategory(categories, 'a', -1)).toBeNull();
    expect(moveCategory(categories, 'c', 1)).toBeNull();
  });

  it('неизвестную категорию не двигает', () => {
    expect(moveCategory(categories, 'zzz', 1)).toBeNull();
  });

  // Порядок отправляется целиком, поэтому исходный массив трогать нельзя: экран рисуется из него же.
  it('не меняет исходный список', () => {
    const before = categories.map((category) => category.categoryId);
    moveCategory(categories, 'b', -1);
    expect(categories.map((category) => category.categoryId)).toEqual(before);
  });
});
