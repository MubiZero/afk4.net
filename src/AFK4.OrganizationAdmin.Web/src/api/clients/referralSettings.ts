import { PlatformApiClient } from '../../platformApi';
import type { ReferralSettingsDto } from '@afk4/contracts';
export type { ReferralSettingsDto } from '@afk4/contracts';

export function createReferralSettingsClient(api: PlatformApiClient) {
  return {
    get(): Promise<ReferralSettingsDto> {
      return api.get<ReferralSettingsDto>('referral-settings');
    },
    update(request: ReferralSettingsDto): Promise<ReferralSettingsDto> {
      return api.post<ReferralSettingsDto, ReferralSettingsDto>('referral-settings', request);
    }
  };
}
