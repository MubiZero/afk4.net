import { PlatformApiClient } from '../../platformApi';

export interface LoyaltySettingsDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
}

export function createLoyaltySettingsClient(api: PlatformApiClient) {
  return {
    get(): Promise<LoyaltySettingsDto> {
      return api.get<LoyaltySettingsDto>('/api/owner/loyalty-settings');
    },
    update(request: LoyaltySettingsDto): Promise<LoyaltySettingsDto> {
      return api.post<LoyaltySettingsDto, LoyaltySettingsDto>('/api/owner/loyalty-settings', request);
    }
  };
}
