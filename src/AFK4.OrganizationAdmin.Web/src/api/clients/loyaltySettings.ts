import { PlatformApiClient } from '../../platformApi';
import type { LoyaltySettingsDto } from '@afk4/contracts';
export type { LoyaltySettingsDto } from '@afk4/contracts';

export function createLoyaltySettingsClient(api: PlatformApiClient) {
  return {
    get(): Promise<LoyaltySettingsDto> {
      return api.get<LoyaltySettingsDto>('loyalty-settings');
    },
    update(request: LoyaltySettingsDto): Promise<LoyaltySettingsDto> {
      return api.post<LoyaltySettingsDto, LoyaltySettingsDto>('loyalty-settings', request);
    }
  };
}
