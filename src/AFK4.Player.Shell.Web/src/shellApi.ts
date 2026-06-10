import type { ExtendSessionRequest, PackageOptionDto, PlayerLoyaltyDto, PlayerNewsItemDto, PlayerTopUpIntentDto, ShopCatalogItemDto, ShopOrderDto, ShopOrderLineInput, TariffOptionDto } from './apiTypes';

export class OfflineError extends Error {
  constructor() { super('offline'); this.name = 'OfflineError'; }
}

export class ApiError extends Error {
  constructor(public status: number, message: string, public code?: string) { super(message); this.name = 'ApiError'; }
}

type FetchLike = (url: string, init?: RequestInit) => Promise<Response>;

function newKey(): string {
  return (globalThis.crypto?.randomUUID?.() ?? `k-${Date.now()}-${Math.floor(performance.now())}`);
}

export function createShellApi(baseUrl: string, fetchImpl: FetchLike = fetch) {
  const base = baseUrl.replace(/\/$/, '');

  async function call<T>(path: string, init?: RequestInit): Promise<T> {
    let response: Response;
    try {
      response = await fetchImpl(`${base}${path}`, {
        ...init,
        headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) }
      });
    } catch {
      throw new OfflineError();
    }
    if (!response.ok) {
      let code: string | undefined;
      try { code = ((await response.clone().json()) as { error?: string }).error; } catch { /* no json body */ }
      throw new ApiError(response.status, `request to ${path} failed: ${response.status}`, code);
    }
    return (await response.json()) as T;
  }

  return {
    listTariffs: (branchId: string) => call<TariffOptionDto[]>(`/api/me/branches/${branchId}/tariffs`),
    listPackages: (branchId: string) => call<PackageOptionDto[]>(`/api/me/branches/${branchId}/packages`),
    createTopUpIntent: (amountMinorUnits: number, currencyCode = 'TJS') =>
      call<PlayerTopUpIntentDto>('/api/me/wallet/top-up-intent', {
        method: 'POST',
        body: JSON.stringify({ amountMinorUnits, currencyCode, method: 'dcgate' })
      }),
    getTopUpIntents: () => call<PlayerTopUpIntentDto[]>('/api/me/wallet/top-up-intents'),
    extendSession: (sessionId: string, req: Omit<ExtendSessionRequest, 'idempotencyKey'> & { idempotencyKey?: string }) =>
      call<unknown>(`/api/me/sessions/${sessionId}/extend`, {
        method: 'POST',
        body: JSON.stringify({ ...req, idempotencyKey: req.idempotencyKey ?? newKey() })
      }),
    listShopCatalog: () => call<ShopCatalogItemDto[]>('/api/me/shop/catalog'),
    placeShopOrder: (lines: ShopOrderLineInput[]) =>
      call<ShopOrderDto>('/api/me/shop/orders', { method: 'POST', body: JSON.stringify({ lines }) }),
    listShopOrders: () => call<ShopOrderDto[]>('/api/me/shop/orders'),
    cancelShopOrder: (orderId: string) =>
      call<ShopOrderDto>(`/api/me/shop/orders/${orderId}/cancel`, { method: 'POST' }),
    getLoyalty: () => call<PlayerLoyaltyDto>('/api/me/loyalty'),
    getNews: () => call<PlayerNewsItemDto[]>('/api/me/news')
  };
}

export type ShellApi = ReturnType<typeof createShellApi>;
