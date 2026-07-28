import { PlatformApiClient } from '../../platformApi';

export interface LoyaltySettingsDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
  sessionEnabled: boolean;
  sessionPercentBasisPoints: number;
  // Limits applied to every accrual; 0 disables the limit. Minor units.
  cashbackCapMinorUnits: number;
  minimumSourceMinorUnits: number;
}

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
