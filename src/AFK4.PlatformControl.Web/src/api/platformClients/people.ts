import type { PlatformTransport } from '../platformTransport';
import type { NetworkPerson } from '../types';

export class NetworkPeopleApi {
  public constructor(private readonly transport: PlatformTransport) {}

  /** Только по точному номеру: списка людей сети в панели нет и быть не должно. */
  public lookupPerson(phoneNumber: string): Promise<NetworkPerson> {
    return this.transport.send<NetworkPerson>('POST', '/api/platform/people/lookup', { phoneNumber });
  }

  public banPerson(platformPersonId: string, reason: string): Promise<NetworkPerson> {
    return this.transport.send<NetworkPerson>(
      'POST', `/api/platform/people/${encodeURIComponent(platformPersonId)}/network-ban`, { reason });
  }

  public liftBan(platformPersonId: string): Promise<NetworkPerson> {
    return this.transport.send<NetworkPerson>(
      'DELETE', `/api/platform/people/${encodeURIComponent(platformPersonId)}/network-ban`);
  }
}
