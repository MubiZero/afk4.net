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

export function createMoneyActionClient(api: PlatformApiClient) {
  return {
    listPending(branchId: Guid): Promise<MoneyActionRequestListResponse> {
      return api.get<MoneyActionRequestListResponse>(`/api/branches/${branchId}/money-actions`);
    },
    approve(branchId: Guid, requestId: Guid, request: MoneyActionDecisionRequest): Promise<MoneyActionDecisionResponse> {
      return api.post<MoneyActionDecisionResponse, MoneyActionDecisionRequest>(`/api/branches/${branchId}/money-actions/${requestId}/approve`, request);
    },
    reject(branchId: Guid, requestId: Guid, request: MoneyActionDecisionRequest): Promise<MoneyActionDecisionResponse> {
      return api.post<MoneyActionDecisionResponse, MoneyActionDecisionRequest>(`/api/branches/${branchId}/money-actions/${requestId}/reject`, request);
    }
  };
}
