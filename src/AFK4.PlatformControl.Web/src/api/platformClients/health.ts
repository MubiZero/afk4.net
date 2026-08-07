import type { PlatformTransport } from '../platformTransport';
import type { HealthOverview } from '../types';

export class HealthApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getOverview(): Promise<HealthOverview> {
    return this.transport.send<HealthOverview>('GET', '/api/platform/health/overview');
  }
}
