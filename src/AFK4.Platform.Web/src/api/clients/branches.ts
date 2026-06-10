import type { ApiTransport } from '../apiTransport';
import type {
  BranchProfile,
  BranchSettings,
  OperatorDashboardSummary,
  UpdateBranchProfileRequest,
  UpdateBranchSettingsRequest
} from '../types';

export class BranchApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getBranchProfile(branchId: string): Promise<BranchProfile> {
    return this.transport.send<BranchProfile>('GET', `/api/branches/${encodeURIComponent(branchId)}/profile`);
  }

  public updateBranchProfile(branchId: string, request: UpdateBranchProfileRequest): Promise<BranchProfile> {
    return this.transport.send<BranchProfile>('PATCH', `/api/branches/${encodeURIComponent(branchId)}/profile`, request);
  }

  public getBranchSettings(branchId: string): Promise<BranchSettings> {
    return this.transport.send<BranchSettings>('GET', `/api/branches/${encodeURIComponent(branchId)}/settings`);
  }

  public updateBranchSettings(branchId: string, request: UpdateBranchSettingsRequest): Promise<BranchSettings> {
    return this.transport.send<BranchSettings>('PUT', `/api/branches/${encodeURIComponent(branchId)}/settings`, request);
  }

  public getDashboardSummary(branchId: string): Promise<OperatorDashboardSummary> {
    const now = new Date();
    const from = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 0, 0, 0));
    const to = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 23, 59, 59));
    const query = new URLSearchParams({
      fromUtc: from.toISOString(),
      toUtc: to.toISOString(),
      limit: '3'
    });
    return this.transport.send<OperatorDashboardSummary>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/dashboard/summary?${query.toString()}`
    );
  }

  public getDashboardSummaryForRange(branchId: string, fromUtc: string, toUtc: string): Promise<OperatorDashboardSummary> {
    const query = new URLSearchParams({ fromUtc, toUtc, limit: '3' });
    return this.transport.send<OperatorDashboardSummary>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/dashboard/summary?${query.toString()}`
    );
  }
}
