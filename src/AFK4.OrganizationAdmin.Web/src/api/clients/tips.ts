import { PlatformApiClient } from '../../platformApi';
import type { OwedShiftTipsDto, ShiftTipsDto, TipSettingsDto, UpdateTipSettingsRequest } from '@afk4/contracts';
export type { OwedShiftTipsDto, ShiftTipDto, ShiftTipsDto, TipSettingsDto } from '@afk4/contracts';

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
    // Ключа нет: сервер выводит его из уже выданного, повтор выдачи дважды не выдаст.
    payOut(shiftId: string): Promise<ShiftTipsDto> {
      return api.post<ShiftTipsDto, Record<string, never>>(`shifts/${shiftId}/tips/payout`, {});
    },
    // Невыданные чаевые закрытых смен филиала: выдают из кассы открытой смены.
    owed(branchId: string): Promise<OwedShiftTipsDto[]> {
      return api.get<OwedShiftTipsDto[]>(`branches/${branchId}/tips/owed`);
    }
  };
}
