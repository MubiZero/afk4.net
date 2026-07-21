import type { MessageKey } from '@afk4/i18n';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';

const DEFAULT_OPEN = '10:00';
const DEFAULT_CLOSE = '22:00';

// i18n-ключи названий дней (1=Пн … 7=Вс), см. Task 4.
export const WEEKDAY_KEY: Record<number, MessageKey> = {
  1: 'op.club.weekday.1',
  2: 'op.club.weekday.2',
  3: 'op.club.weekday.3',
  4: 'op.club.weekday.4',
  5: 'op.club.weekday.5',
  6: 'op.club.weekday.6',
  7: 'op.club.weekday.7'
};

export function defaultWorkingHours(): BranchWorkingHoursDay[] {
  return [1, 2, 3, 4, 5, 6, 7].map((dayOfWeek) => ({
    dayOfWeek,
    isClosed: false,
    openTime: DEFAULT_OPEN,
    closeTime: DEFAULT_CLOSE
  }));
}

// Всегда нормализует к 7 дням 1..7: провайдер (сервер/state) мог прислать частичный/пустой набор.
export function normalizeWorkingHours(raw: unknown): BranchWorkingHoursDay[] {
  const byDay = new Map<number, BranchWorkingHoursDay>();
  if (Array.isArray(raw)) {
    for (const item of raw as BranchWorkingHoursDay[]) {
      if (item && typeof item.dayOfWeek === 'number' && item.dayOfWeek >= 1 && item.dayOfWeek <= 7) {
        byDay.set(item.dayOfWeek, {
          dayOfWeek: item.dayOfWeek,
          isClosed: Boolean(item.isClosed),
          openTime: item.openTime ?? DEFAULT_OPEN,
          closeTime: item.closeTime ?? DEFAULT_CLOSE
        });
      }
    }
  }
  return [1, 2, 3, 4, 5, 6, 7].map(
    (dayOfWeek) =>
      byDay.get(dayOfWeek) ?? { dayOfWeek, isClosed: false, openTime: DEFAULT_OPEN, closeTime: DEFAULT_CLOSE }
  );
}
