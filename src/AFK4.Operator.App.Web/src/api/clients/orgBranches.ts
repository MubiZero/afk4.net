import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface OwnerBranchSummary {
  branchId: Guid;
  name: string;
}

// Org-wide branch list for Owner-only screens (Сеть → Филиалы) — same endpoint the News «owner
// picker» uses (OrganizationPermissionNames.ViewBranches gate lives on the API side).
export function createOrgBranchesClient(api: PlatformApiClient) {
  return {
    getOwnerBranches(): Promise<OwnerBranchSummary[]> {
      return api.get<OwnerBranchSummary[]>('/api/owner/branches');
    }
  };
}
