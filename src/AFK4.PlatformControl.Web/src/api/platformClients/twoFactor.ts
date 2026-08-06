import type { PlatformTransport } from '../platformTransport';

// Thin facade over the transport's 2FA methods (which own session-application on success — see
// PlatformTransport) plus the one authenticated route in this family: an admin resetting a
// colleague's 2FA under platform.admins.manage.
export class TwoFactorApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public beginSetup(challengeToken: string) {
    return this.transport.beginTwoFactorSetup(challengeToken);
  }

  public completeSetup(challengeToken: string, code: string) {
    return this.transport.completeTwoFactorSetup(challengeToken, code);
  }

  public verify(challengeToken: string, code: string) {
    return this.transport.completeTwoFactor(challengeToken, code);
  }

  public async reset(platformAdminUserId: string): Promise<void> {
    await this.transport.send<void>('POST', `/api/platform/admins/${platformAdminUserId}/2fa/reset`);
  }
}
