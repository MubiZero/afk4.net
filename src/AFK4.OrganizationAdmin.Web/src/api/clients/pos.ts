import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';
import type { PaymentPartDto } from './sessions';

/** Строка чека, как её отдаёт сервер (PosSaleLineDto). Поля сверяются в `contractParity.test.ts`. */
export interface PosSaleLineDto {
  productId: Guid;
  productName: string;
  quantity: number;
  unitPrice: MoneyDto;
  lineTotal: MoneyDto;
}

// В запросе на создание чека клиент шлёт только то, что знает сам: имя товара и сумму строки
// сервер подставляет из каталога, а не верит присланному. Поэтому тип запроса свой, а не PosSaleLineDto.
export interface CreatePosSaleLineDto {
  productId: Guid;
  quantity: number;
  unitPrice: MoneyDto;
}

export interface CreatePosSaleRequest {
  organizationId: Guid;
  shiftId: Guid;
  lines: CreatePosSaleLineDto[];
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

/** Чек бара (PosSaleDto). Поля сверяются в `contractParity.test.ts`. */
export interface PosSaleDto {
  posSaleId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  state: string;
  lines: PosSaleLineDto[];
  total: MoneyDto;
  createdByStaffUserId: Guid;
  createdAtUtc: string;
  paidAtUtc: string | null;
  refundedAtUtc: string | null;
  voidedAtUtc: string | null;
  latestReceipt?: ReceiptDto | null;
  playerAccountId?: Guid | null;
  shopOrderId?: Guid | null;
  /** Чем заплатили. Пусто у черновика — его ещё не оплачивали. */
  payments?: PaymentPartDto[] | null;
}

export interface ReceiptDto {
  receiptId: Guid;
  organizationId: Guid;
  branchId: Guid;
  posSaleId: Guid | null;
  receiptNumber: string;
  receiptType: string;
  total: MoneyDto;
  createdAtUtc: string;
  sessionId?: Guid | null;
  shopOrderId?: Guid | null;
}

export interface PosProductCategoryDto {
  categoryId: Guid;
  organizationId: Guid;
  branchId: Guid;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export function createPosClient(api: PlatformApiClient) {
  return {
    getCatalog(branchId: Guid): Promise<PosProductDto[]> {
      return api.get<PosProductDto[]>(`branches/${branchId}/pos/catalog`);
    },
    createSale(branchId: Guid, request: CreatePosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, CreatePosSaleRequest>(`branches/${branchId}/pos/sales`, request);
    },
    paySaleManual(saleId: Guid, request: ManualPaymentRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, ManualPaymentRequest>(`pos/sales/${saleId}/payments/manual`, request);
    },
    settleSale(saleId: Guid, request: SettlePosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, SettlePosSaleRequest>(`pos/sales/${saleId}/settlements`, request);
    },
    refundSale(saleId: Guid, request: RefundPosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, RefundPosSaleRequest>(`pos/sales/${saleId}/refunds`, request);
    },
    voidSale(saleId: Guid, request: VoidPosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, VoidPosSaleRequest>(`pos/sales/${saleId}/void`, request);
    },
    getSale(saleId: Guid): Promise<PosSaleDto> {
      return api.get<PosSaleDto>(`pos/sales/${saleId}`);
    },
    getReceipt(receiptId: Guid): Promise<ReceiptDto> {
      return api.get<ReceiptDto>(`receipts/${receiptId}`);
    }
  };
}
