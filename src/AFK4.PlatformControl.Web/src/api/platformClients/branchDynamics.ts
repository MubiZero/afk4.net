import type { PlatformTransport } from '../platformTransport';
import type { BranchDynamics } from '../types';

export class BranchDynamicsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getBranchDynamics(organizationId: string, branchId: string, days = 30): Promise<BranchDynamics> {
    return this.transport.send<BranchDynamics>(
      'GET',
      `/api/platform/organizations/${encodeURIComponent(organizationId)}/branches/${encodeURIComponent(branchId)}/dynamics?days=${days}`
    );
  }
}
