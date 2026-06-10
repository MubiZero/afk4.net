import { PlatformApiClient } from '../../platformApi';

export interface StaffPhoneStatusDto {
  phone: string | null;
  phoneVerifiedAtUtc: string | null;
}

export interface StaffPhoneVerificationStartedDto {
  expiresInSeconds: number;
  resendAfterSeconds: number;
}

export interface StaffPhoneConfirmedDto {
  phone: string;
}

export function createAccountClient(api: PlatformApiClient) {
  return {
    getMyPhone(): Promise<StaffPhoneStatusDto> {
      return api.get<StaffPhoneStatusDto>('/api/auth/staff/phone');
    },
    startPhoneVerification(request: { phone: string }): Promise<StaffPhoneVerificationStartedDto> {
      return api.post<StaffPhoneVerificationStartedDto, { phone: string }>(
        '/api/auth/staff/phone/start-verification', request);
    },
    confirmPhoneVerification(request: { code: string }): Promise<StaffPhoneConfirmedDto> {
      return api.post<StaffPhoneConfirmedDto, { code: string }>(
        '/api/auth/staff/phone/confirm', request);
    }
  };
}
