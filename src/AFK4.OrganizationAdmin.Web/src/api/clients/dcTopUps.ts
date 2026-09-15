import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { CreateDcTopUpRequest, DcTopUpDto } from '@afk4/contracts';
export type { CreateDcTopUpRequest, DcTopUpDto } from '@afk4/contracts';

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
