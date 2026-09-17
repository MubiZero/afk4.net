import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type {
  CreatePosSaleRequest,
  PosProductDto,
  PosSaleDto,
  ReceiptDto,
  RefundPosSaleRequest,
  SettlePosSaleRequest,
  VoidPosSaleRequest,
} from '@afk4/contracts';
export type {
  CreatePosSaleLineDto,
  CreatePosSaleRequest,
  ManualPaymentRequest,
  PosProductCategoryDto,
  PosProductDto,
  PosSaleDto,
  PosSaleLineDto,
  ReceiptDto,
  RefundPosSaleRequest,
  SettlePosSaleRequest,
  VoidPosSaleRequest,
} from '@afk4/contracts';

export function createPosClient(api: PlatformApiClient) {
  return {
    getCatalog(branchId: Guid): Promise<PosProductDto[]> {
      return api.get<PosProductDto[]>(`branches/${branchId}/pos/catalog`);
    },
    createSale(branchId: Guid, request: CreatePosSaleRequest): Promise<PosSaleDto> {
      return api.post<PosSaleDto, CreatePosSaleRequest>(`branches/${branchId}/pos/sales`, request);
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
