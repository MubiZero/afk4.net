import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface MoneyActionRequestDto {
  moneyActionRequestId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  actionType: string;
  requestedByStaffUserId: Guid;
  amountMinorUnits: number;
  currencyCode: string;
  reason: string;
  state: string;
  createdAtUtc: string;
  expiresAtUtc: string;
}

export interface MoneyActionRequestListResponse {
  requests: MoneyActionRequestDto[];
}

export interface MoneyActionDecisionRequest extends Record<string, unknown> {
  decisionReason?: string | null;
}

export type MoneyActionDecisionResponse = Record<string, unknown>;

/// Заявка на денежную операцию, которую сотруднику не даёт провести его порог. Форма повторяет
/// серверный MoneyActionSubmitRequest: одобренная заявка выполняется сервером сама, поэтому
/// в ней должно лежать всё, что нужно для исполнения, а не ссылка на «то, что хотели».
export interface MoneyActionSubmitRequest extends Record<string, unknown> {
  organizationId: Guid;
  actionType: 'refund' | 'manual_correction';
  playerAccountId: Guid;
  ledgerEntryId: Guid | null;
  accountType: string;
  signedAmountMinorUnits: number;
  currencyCode: string;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

export interface MoneyActionSubmitResponse extends Record<string, unknown> {
  outcome: string;
}

export function createMoneyActionClient(api: PlatformApiClient) {
  return {
    // Половина механизма антифрода была мертва: очередь одобрений умела принять и отклонить
    // заявку, но подать её было неоткуда, а сервер при превышении порога отвечал «подайте
    // через /money-actions» — то есть советовал экран, которого не существовало.
    submit(branchId: Guid, request: MoneyActionSubmitRequest): Promise<MoneyActionSubmitResponse> {
      return api.post<MoneyActionSubmitResponse, MoneyActionSubmitRequest>(`branches/${branchId}/money-actions`, request);
    },
    listPending(branchId: Guid): Promise<MoneyActionRequestListResponse> {
      return api.get<MoneyActionRequestListResponse>(`branches/${branchId}/money-actions`);
    },
    approve(branchId: Guid, requestId: Guid, request: MoneyActionDecisionRequest): Promise<MoneyActionDecisionResponse> {
      return api.post<MoneyActionDecisionResponse, MoneyActionDecisionRequest>(`branches/${branchId}/money-actions/${requestId}/approve`, request);
    },
    reject(branchId: Guid, requestId: Guid, request: MoneyActionDecisionRequest): Promise<MoneyActionDecisionResponse> {
      return api.post<MoneyActionDecisionResponse, MoneyActionDecisionRequest>(`branches/${branchId}/money-actions/${requestId}/reject`, request);
    }
  };
}
