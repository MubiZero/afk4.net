import type { ApiTransport } from '../apiTransport';

export class ProfileApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getStaffPhone(): Promise<{ phone: string | null; phoneVerifiedAtUtc: string | null }> {
    return this.transport.send<{ phone: string | null; phoneVerifiedAtUtc: string | null }>('GET', '/api/auth/staff/phone');
  }

  public startPhoneVerification(phone: string): Promise<{ expiresInSeconds: number; resendAfterSeconds: number }> {
    return this.transport.send<{ expiresInSeconds: number; resendAfterSeconds: number }>('POST', '/api/auth/staff/phone/start-verification', { phone });
  }

  public confirmPhoneVerification(code: string): Promise<{ phone: string }> {
    return this.transport.send<{ phone: string }>('POST', '/api/auth/staff/phone/confirm', { code });
  }
}
