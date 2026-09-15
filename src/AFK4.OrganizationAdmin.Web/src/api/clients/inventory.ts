import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

/** Движение товара на складе (StockMovementDto). Поля сверяются в `contractParity.test.ts`. */
export interface StockMovementDto {
  stockMovementId: Guid;
  organizationId: Guid;
  branchId: Guid;
  productId: Guid;
  movementType: string;
  quantityDelta: number;
  unitCost: MoneyDto;
  reason: string;
  createdByStaffUserId: Guid;
  createdAtUtc: string;
  createdByDisplayName?: string | null;
}

export type StockMovementSearchQuery = ReportQuery & {
  productId?: Guid | null;
};

export interface CreateStockMovementRequest extends Record<string, unknown> {
  organizationId: Guid;
  productId: Guid;
  movementType: string;
  quantityDelta: number;
  unitCost: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export function createInventoryClient(api: PlatformApiClient) {
  return {
    getStockMovements(branchId: Guid, query?: StockMovementSearchQuery): Promise<StockMovementDto[]> {
      return api.get<StockMovementDto[]>(`branches/${branchId}/inventory/stock-movements`, normalizeReportQuery(query));
    },
    createStockMovement(branchId: Guid, request: CreateStockMovementRequest): Promise<StockMovementDto> {
      return api.post<StockMovementDto, CreateStockMovementRequest>(`branches/${branchId}/inventory/stock-movements`, request);
    }
  };
}
