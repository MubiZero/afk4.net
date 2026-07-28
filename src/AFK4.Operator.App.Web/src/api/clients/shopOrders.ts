import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

export interface ShopOrderLineDto {
  productId: Guid;
  name: string;
  unitPrice: MoneyDto;
  quantity: number;
  lineTotal: MoneyDto;
}

export interface ShopOrderDto {
  id: Guid;
  branchId: Guid;
  seatId: Guid;
  playerAccountId: Guid;
  playerDisplayName: string;
  status: string;
  total: MoneyDto;
  lines: ShopOrderLineDto[];
  placedAtUtc: string;
  acceptedAtUtc: string | null;
  deliveredAtUtc: string | null;
  cancelledAtUtc: string | null;
  version: number;
}

export function createShopOrderClient(api: PlatformApiClient) {
  return {
    listQueue(branchId: Guid): Promise<ShopOrderDto[]> {
      return api.get<ShopOrderDto[]>(`branches/${branchId}/shop/orders`);
    },
    accept(branchId: Guid, orderId: Guid, expectedVersion: number): Promise<ShopOrderDto> {
      return api.post<ShopOrderDto, { expectedVersion: number }>(`branches/${branchId}/shop/orders/${orderId}/accept`, { expectedVersion });
    },
    deliver(branchId: Guid, orderId: Guid, expectedVersion: number): Promise<ShopOrderDto> {
      return api.post<ShopOrderDto, { expectedVersion: number }>(`branches/${branchId}/shop/orders/${orderId}/deliver`, { expectedVersion });
    },
    cancel(branchId: Guid, orderId: Guid, expectedVersion: number): Promise<ShopOrderDto> {
      return api.post<ShopOrderDto, { expectedVersion: number }>(`branches/${branchId}/shop/orders/${orderId}/cancel`, { expectedVersion });
    }
  };
}
