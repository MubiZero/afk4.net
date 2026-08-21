import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

export interface ShiftRevenueDto {
  shiftId: string;
  organizationId: string;
  branchId: string;
  openedByStaffUserId: string;
  closedByStaffUserId: string | null;
  state: string;
  // noShow — удержанная за неявку предоплата. Стоит отдельно от time намеренно: это не проданное
  // время, но в total оно входит — деньги смена заработала.
  earned: { time: MoneyDto; goods: MoneyDto; noShow: MoneyDto; total: MoneyDto };
  inflow: { cash: MoneyDto; nonCash: MoneyDto; walletTopUps: MoneyDto; directTotal: MoneyDto };
  cash: { starting: MoneyDto; expected: MoneyDto; counted: MoneyDto | null; difference: MoneyDto | null };
  openedAtUtc: string;
  closedAtUtc: string | null;
}

export interface ShiftRevenueListDto {
  shifts: ShiftRevenueDto[];
  limit: number;
}

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
