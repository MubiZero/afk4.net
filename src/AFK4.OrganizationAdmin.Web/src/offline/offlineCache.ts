import type { TariffOptionDto } from '@afk4/contracts';

/**
 * Что Панель помнит о филиале на случай обрыва связи: тарифы (цену гостю без сервера называет она же)
 * и открытую смену (деньги без связи ложатся в неё). Пишется, пока связь есть.
 */
export interface OfflineDeskCache {
  tariffs: TariffOptionDto[];
  shiftId: string | null;
  cachedAtMs: number;
}

const key = (branchId: string) => `afk4.organization-admin.offlineDesk.${branchId}`;

export function readOfflineDeskCache(branchId: string): OfflineDeskCache | null {
  try {
    const raw = window.localStorage.getItem(key(branchId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as OfflineDeskCache;
    return Array.isArray(parsed.tariffs) && typeof parsed.cachedAtMs === 'number' ? parsed : null;
  } catch {
    return null;
  }
}

export function writeOfflineDeskCache(branchId: string, cache: OfflineDeskCache): void {
  try {
    window.localStorage.setItem(key(branchId), JSON.stringify(cache));
  } catch {
    // Квота или запрет хранилища: без кэша офлайн-посадки просто не будет — это видно по кнопке.
  }
}
