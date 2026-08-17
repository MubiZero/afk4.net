import { PlatformApiClient } from '../../platformApi';

export interface ReferralSettingsDto {
  enabled: boolean;
  // Суммы в минорных единицах, как и везде в деньгах.
  referrerBonusMinorUnits: number;
  inviteeBonusMinorUnits: number;
  // Пополнение меньше этой суммы бонус не запускает.
  minimumTopUpMinorUnits: number;
  // Сколько дней после заведения аккаунта друг может назвать код; 0 — окна нет.
  claimWindowDays: number;
  // Сколько друзей одного игрока оплачивается; 0 — без ограничения.
  maxRewardedPerReferrer: number;
}

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
