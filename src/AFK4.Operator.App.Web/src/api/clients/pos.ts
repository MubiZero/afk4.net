import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';
import type { PaymentPartDto } from './sessions';

export interface PosSaleLineDto {
  productId: Guid;
  quantity: number;
  unitPrice: MoneyDto;
}

export interface CreatePosSaleRequest {
  organizationId: Guid;
  shiftId: Guid;
  lines: PosSaleLineDto[];
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  // When set, the sale joins an open session tab and is settled at checkout.
  sessionId?: Guid | null;
}

export interface ManualPaymentRequest {
  organizationId: Guid;
  paymentMethod: string;
  amount: MoneyDto;
  note: string;
  idempotencyKey: string;
}

export interface SettlePosSaleRequest {
  organizationId: Guid;
  payments: PaymentPartDto[];
  note: string;
  idempotencyKey: string;
}

export interface RefundPosSaleRequest {
  organizationId: Guid;
  reason: string;
  idempotencyKey: string;
}

export interface VoidPosSaleRequest {
  organizationId: Guid;
  reason: string;
  idempotencyKey: string;
}

export interface PosProductDto extends Record<string, unknown> {
  productId: Guid;
  name: string;
  price: MoneyDto;
  availableInShell?: boolean;
}

export type PosSaleDto = Record<string, unknown>;
export type ReceiptDto = Record<string, unknown>;
export type PosProductCategoryDto = Record<string, unknown>;

export function createPosClient(api: PlatformApiClient) {
  return {
    getCatalog(branchId: Guid): Promise<PosProductDto[]> {
      return api.get<PosProductDto[]>(`/api/branches/${branchId}/pos/catalog`);
    },
    createSale(branchId: Guid, request: CreatePosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, CreatePosSaleRequest>(`/api/branches/${branchId}/pos/sales`, request);
    },
    paySaleManual(saleId: Guid, request: ManualPaymentRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, ManualPaymentRequest>(`/api/pos/sales/${saleId}/payments/manual`, request);
    },
    settleSale(saleId: Guid, request: SettlePosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, SettlePosSaleRequest>(`/api/pos/sales/${saleId}/settlements`, request);
    },
    refundSale(saleId: Guid, request: RefundPosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, RefundPosSaleRequest>(`/api/pos/sales/${saleId}/refunds`, request);
    },
    voidSale(saleId: Guid, request: VoidPosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, VoidPosSaleRequest>(`/api/pos/sales/${saleId}/void`, request);
    },
    getSale(saleId: Guid): Promise<PosSaleDto> {
      return api.get<PosSaleDto>(`/api/pos/sales/${saleId}`);
    },
    getReceipt(receiptId: Guid): Promise<ReceiptDto> {
      return api.get<ReceiptDto>(`/api/receipts/${receiptId}`);
    }
  };
}
