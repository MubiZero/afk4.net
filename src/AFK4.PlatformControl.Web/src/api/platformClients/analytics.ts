import type { PlatformTransport } from '../platformTransport';
import type { AnalyticsOverview } from '../types';

export class AnalyticsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getOverview(months = 12): Promise<AnalyticsOverview> {
    return this.transport.send<AnalyticsOverview>('GET', `/api/platform/analytics/overview?months=${months}`);
  }
}
