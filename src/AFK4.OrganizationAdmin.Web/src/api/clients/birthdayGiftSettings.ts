import { PlatformApiClient } from '../../platformApi';
import type { BirthdayGiftSettingsDto } from '@afk4/contracts';
export type { BirthdayGiftSettingsDto } from '@afk4/contracts';

// Подарок на день рождения: сумма на баланс в сам день и кому он положен.
export function createBirthdayGiftSettingsClient(api: PlatformApiClient) {
  return {
    get(): Promise<BirthdayGiftSettingsDto> {
      return api.get<BirthdayGiftSettingsDto>('birthday-gift-settings');
    },
    update(request: BirthdayGiftSettingsDto): Promise<BirthdayGiftSettingsDto> {
      return api.post<BirthdayGiftSettingsDto, BirthdayGiftSettingsDto>('birthday-gift-settings', request);
    }
  };
}
