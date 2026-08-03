import type { PlatformTransport } from '../platformTransport';
import type { PlatformPulse } from '../types';

export class PulseApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getPulse(): Promise<PlatformPulse> {
    return this.transport.send<PlatformPulse>('GET', '/api/platform/pulse');
  }
}
