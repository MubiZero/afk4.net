import type { PlatformTransport } from '../platformTransport';
import type {
  CreatePlatformUpdatePackageRequest,
  CreatePlatformUpdateRolloutRequest,
  PlatformUpdatePackage,
  PlatformUpdateRollout
} from '../types';

export class UpdatesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listPackages(): Promise<PlatformUpdatePackage[]> {
    return this.transport.send('GET', '/api/platform/updates/packages');
  }

  public registerPackage(request: CreatePlatformUpdatePackageRequest): Promise<PlatformUpdatePackage> {
    return this.transport.send('POST', '/api/platform/updates/packages', request);
  }

  public changePackageState(packageId: string, state: string, reason: string): Promise<PlatformUpdatePackage> {
    return this.transport.send('POST', `/api/platform/updates/packages/${encodeURIComponent(packageId)}/state`, { state, reason });
  }

  public listRollouts(): Promise<PlatformUpdateRollout[]> {
    return this.transport.send('GET', '/api/platform/updates/rollouts');
  }

  public createRollout(request: CreatePlatformUpdateRolloutRequest): Promise<PlatformUpdateRollout> {
    return this.transport.send('POST', '/api/platform/updates/rollouts', request);
  }

  public changeRolloutState(rolloutId: string, state: string, reason: string): Promise<PlatformUpdateRollout> {
    return this.transport.send('POST', `/api/platform/updates/rollouts/${encodeURIComponent(rolloutId)}/state`, { state, reason });
  }
}
