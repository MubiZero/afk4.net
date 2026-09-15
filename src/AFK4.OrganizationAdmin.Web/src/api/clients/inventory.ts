import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type { CreateStockMovementRequest, StockMovementDto } from '@afk4/contracts';
export type { CreateStockMovementRequest, StockMovementDto } from '@afk4/contracts';

export type StockMovementSearchQuery = ReportQuery & {
  productId?: Guid | null;
};

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
