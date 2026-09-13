import type { PlatformTransport } from '../platformTransport';
import type { HealthOverview, TestEmailResult } from '../types';

export class HealthApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getOverview(): Promise<HealthOverview> {
    return this.transport.send<HealthOverview>('GET', '/api/platform/health/overview');
  }

  public sendTestEmail(email: string): Promise<TestEmailResult> {
    return this.transport.send<TestEmailResult>('POST', '/api/platform/health/test-email', { email });
  }
}
