import type { PlatformTransport } from '../platformTransport';
import type { DebtRow } from '../types';

export class DebtApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listDebt(): Promise<DebtRow[]> {
    return this.transport.send<DebtRow[]>('GET', '/api/platform/debt');
  }
}
