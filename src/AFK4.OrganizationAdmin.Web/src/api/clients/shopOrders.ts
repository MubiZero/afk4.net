import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { ShopOrderDto } from '@afk4/contracts';
export type { ShopOrderDto, ShopOrderLineDto } from '@afk4/contracts';

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
