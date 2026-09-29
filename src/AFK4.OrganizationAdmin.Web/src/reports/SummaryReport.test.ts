import { describe, it, expect } from 'bun:test';
import { trendDayLabel } from './SummaryReport';

// Точка тренда несёт календарную дату (IsoDate), не момент: раньше её дописывали до
// `${date}T00:00:00Z` и пропускали через форматтер без явного пояса — дата "плыла" на браузере
// зрителя (23 сент. по клубу превращалось в "24 сент., 05:00" по Душанбе). trendDayLabel фиксирует
// UTC явно и не показывает время; год — только если отличается от текущего.
describe('trendDayLabel', () => {
  it('shows just the day and month for the current year', () => {
    const currentYear = new Date().getUTCFullYear();
    expect(trendDayLabel(`${currentYear}-09-23`, 'ru')).toBe('23 сент.');
  });

  it('adds the year when it differs from the current year', () => {
    expect(trendDayLabel('2020-01-05', 'ru')).toBe('5 янв. 2020 г.');
  });

  it('does not shift the date across a viewer time zone', () => {
    // Пояс исполнения теста может быть любым (CI — UTC, машина разработчика — нет): без
    // timeZone: 'UTC' внутри форматтера полночь по UTC могла съехать на соседний день.
    expect(trendDayLabel('2026-09-23', 'ru')).toBe('23 сент.');
  });
});
