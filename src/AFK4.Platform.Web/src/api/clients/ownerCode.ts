import type { ApiTransport } from '../apiTransport';
import type { OwnerCodeIssued, OwnerCodeSummary } from '../types';

export class OwnerCodeApi {
  public constructor(private readonly transport: ApiTransport) {}

  public async getOwnerCode(): Promise<OwnerCodeSummary | null> {
    const response = await this.transport.sendRaw('GET', '/api/staff/me/owner-code');
    if (response.status === 204) {
      return null;
    }
    return this.transport.readJson<OwnerCodeSummary>(response);
  }

  public generateOwnerCode(): Promise<OwnerCodeIssued> {
    return this.transport.send<OwnerCodeIssued>('POST', '/api/staff/me/owner-code/generate');
  }

  public rotateOwnerCode(reason: string): Promise<OwnerCodeIssued> {
    return this.transport.send<OwnerCodeIssued>('POST', '/api/staff/me/owner-code/rotate', { reason });
  }
}
