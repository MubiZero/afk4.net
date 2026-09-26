import { PlatformApiClient } from '../../platformApi';
import type { PayOutShiftTipsRequest, ShiftTipsDto, TipSettingsDto, UpdateTipSettingsRequest } from '@afk4/contracts';
export type { ShiftTipDto, ShiftTipsDto, TipSettingsDto } from '@afk4/contracts';

// Чаевые администратору с экрана ПК: настройка клуба и чаевые смены.
export function createTipsClient(api: PlatformApiClient) {
  return {
    getSettings(): Promise<TipSettingsDto> {
      return api.get<TipSettingsDto>('tip-settings');
    },
    updateSettings(request: UpdateTipSettingsRequest): Promise<TipSettingsDto> {
      return api.put<TipSettingsDto, UpdateTipSettingsRequest>('tip-settings', request);
    },
    forShift(shiftId: string): Promise<ShiftTipsDto> {
      return api.get<ShiftTipsDto>(`shifts/${shiftId}/tips`);
    },
    reverse(shiftId: string, ledgerEntryId: string): Promise<ShiftTipsDto> {
      return api.post<ShiftTipsDto, Record<string, never>>(`shifts/${shiftId}/tips/${ledgerEntryId}/reverse`, {});
    },
    payOut(shiftId: string, request: PayOutShiftTipsRequest): Promise<ShiftTipsDto> {
      return api.post<ShiftTipsDto, PayOutShiftTipsRequest>(`shifts/${shiftId}/tips/payout`, request);
    }
  };
}
