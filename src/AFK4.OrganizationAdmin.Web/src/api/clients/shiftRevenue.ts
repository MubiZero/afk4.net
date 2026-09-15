import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { ShiftRevenueDto, ShiftRevenueListDto } from '@afk4/contracts';
export type { ShiftRevenueDto, ShiftRevenueListDto } from '@afk4/contracts';

export function createShiftRevenueClient(api: PlatformApiClient) {
  return {
    current(branchId: Guid): Promise<ShiftRevenueDto | null> {
      return api.getOptional<ShiftRevenueDto>(`branches/${branchId}/shifts/revenue/current`);
    },
    history(branchId: Guid, limit = 20): Promise<ShiftRevenueListDto> {
      return api.get<ShiftRevenueListDto>(`branches/${branchId}/shifts/revenue`, { limit });
    }
  };
}
