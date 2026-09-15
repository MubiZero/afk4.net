import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type {
  MoneyActionDecisionRequest,
  MoneyActionRequestListResponse,
  MoneyActionSubmitRequest,
  MoneyActionSubmitResponse,
} from '@afk4/contracts';
export type {
  MoneyActionDecisionRequest,
  MoneyActionRequestDto,
  MoneyActionRequestListResponse,
  MoneyActionSubmitRequest,
  MoneyActionSubmitResponse,
} from '@afk4/contracts';

// Одобрение и отказ отвечают тем же телом, что и подача заявки (MoneyActionSubmitResponse):
// у сервера для решения отдельной записи нет, и заводить её у клиента значило бы выдумать контракт.
export type MoneyActionDecisionResponse = MoneyActionSubmitResponse;

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
