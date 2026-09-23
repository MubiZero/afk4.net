import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { ShopOrderDto } from '@afk4/contracts';
export type { ShopOrderDto, ShopOrderLineDto } from '@afk4/contracts';

export function createShopOrderClient(api: PlatformApiClient) {
  return {
    listQueue(branchId: Guid): Promise<ShopOrderDto[]> {
      return api.get<ShopOrderDto[]>(`branches/${branchId}/shop/orders`);
    },
    // Один заказ в любом состоянии — для палитры: выданного в ленте уже нет.
    get(branchId: Guid, orderId: Guid): Promise<ShopOrderDto> {
      return api.get<ShopOrderDto>(`branches/${branchId}/shop/orders/${orderId}`);
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
