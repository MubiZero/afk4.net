import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

// DC-пополнение в Кассе: кассир заводит намерение (создаёт pay-link + QR), игрок переводит
// на карту DushanbeCity по ссылке/QR, кассир подтверждает получение. Подтверждение и отмена —
// два разных действия: confirm зовёт уже существующий wallet fulfil-эндпоинт (кредитует кошелёк
// и требует открытую смену), cancel — POS-специфичный эндпоинт, снимающий намерение с pending.
export interface DcTopUpDto {
  intentId: Guid;
  payUrl: string;
  comment: string;
  amountMinorUnits: number;
  currencyCode: string;
  cardLast4: string;
}

export interface CreateDcTopUpRequest {
  playerAccountId: Guid;
  amountMinorUnits: number;
  currencyCode: string;
}

export function createDcTopUpClient(api: PlatformApiClient) {
  return {
    create(branchId: Guid, request: CreateDcTopUpRequest): Promise<DcTopUpDto> {
      return api.post<DcTopUpDto, CreateDcTopUpRequest>(`branches/${branchId}/pos/dc-topups`, request);
    },
    cancel(branchId: Guid, intentId: Guid): Promise<void> {
      return api.post<void, undefined>(`branches/${branchId}/pos/dc-topups/${intentId}/cancel`, undefined);
    },
    // Existing wallet endpoint — the intent id is the idempotency key, so calling this twice
    // (e.g. a retried click) is safe on the server.
    confirm(intentId: Guid): Promise<unknown> {
      return api.post<unknown, undefined>(`wallet/top-up-intents/${intentId}/fulfil`, undefined);
    }
  };
}

export type DcTopUpClient = ReturnType<typeof createDcTopUpClient>;
