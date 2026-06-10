import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface OwnerPaymentGatewayDto {
  branchPaymentGatewayId: Guid;
  branchId: Guid | null;
  dcgateProjectId: string;
  cardLast4: string;
  status: string; // pending_telegram | active | disabled
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface OwnerPaymentGatewayListResponse {
  gateways: OwnerPaymentGatewayDto[];
}

export interface ProvisionPaymentGatewayRequest extends Record<string, unknown> {
  branchId?: Guid | null;
  cardNumber: string;
}

export interface TelegramStartRequest extends Record<string, unknown> {
  phone: string;
}
export interface TelegramStartResponse {
  loginAttemptId: string | null;
  state: string;
}
export interface TelegramVerifyCodeRequest extends Record<string, unknown> {
  loginAttemptId: string;
  code: string;
}
export interface TelegramVerifyPasswordRequest extends Record<string, unknown> {
  loginAttemptId: string;
  password: string;
}
export interface TelegramVerifyResponse {
  state: string;
  gatewayStatus: string;
}

export interface OwnerGatewayStatusResponse {
  gatewayStatus: string;
  sessionHealth: string; // online | offline | configured
  lastConnectedAt: string | null;
  lastMessageAt: string | null;
  telegramMessagesCount: number;
}

export function createPaymentGatewayClient(api: PlatformApiClient) {
  return {
    list(): Promise<OwnerPaymentGatewayListResponse> {
      return api.get<OwnerPaymentGatewayListResponse>('/api/owner/payment-gateways');
    },
    provision(request: ProvisionPaymentGatewayRequest): Promise<OwnerPaymentGatewayDto> {
      return api.post<OwnerPaymentGatewayDto, ProvisionPaymentGatewayRequest>(
        '/api/owner/payment-gateways', request);
    },
    telegramStart(id: Guid, request: TelegramStartRequest): Promise<TelegramStartResponse> {
      return api.post<TelegramStartResponse, TelegramStartRequest>(
        `/api/owner/payment-gateways/${id}/telegram/start`, request);
    },
    telegramVerifyCode(id: Guid, request: TelegramVerifyCodeRequest): Promise<TelegramVerifyResponse> {
      return api.post<TelegramVerifyResponse, TelegramVerifyCodeRequest>(
        `/api/owner/payment-gateways/${id}/telegram/verify-code`, request);
    },
    telegramVerifyPassword(id: Guid, request: TelegramVerifyPasswordRequest): Promise<TelegramVerifyResponse> {
      return api.post<TelegramVerifyResponse, TelegramVerifyPasswordRequest>(
        `/api/owner/payment-gateways/${id}/telegram/verify-password`, request);
    },
    status(id: Guid): Promise<OwnerGatewayStatusResponse> {
      return api.get<OwnerGatewayStatusResponse>(`/api/owner/payment-gateways/${id}/status`);
    },
    disable(id: Guid): Promise<OwnerPaymentGatewayDto> {
      return api.post<OwnerPaymentGatewayDto>(`/api/owner/payment-gateways/${id}/disable`);
    }
  };
}
