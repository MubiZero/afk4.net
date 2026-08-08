import type { PlatformTransport } from '../platformTransport';
import type {
  CreateBranchRequest,
  CreateOrganizationRequest,
  CreateOrganizationResponse,
  OrganizationBranch,
  OrganizationDetail,
  OrganizationHealth,
  OrganizationOwnerInvite,
  OrganizationSummary
} from '../types';

export class OrganizationsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listOrganizations(): Promise<OrganizationSummary[]> {
    return this.transport.send<OrganizationSummary[]>('GET', '/api/platform/organizations');
  }

  public getOrganization(organizationId: string): Promise<OrganizationDetail> {
    return this.transport.send<OrganizationDetail>('GET', `/api/platform/organizations/${organizationId}`);
  }

  public createOrganization(request: CreateOrganizationRequest): Promise<CreateOrganizationResponse> {
    return this.transport.send<CreateOrganizationResponse>('POST', '/api/platform/organizations', request);
  }

  public updateStatus(organizationId: string, status: string, reason: string): Promise<OrganizationDetail> {
    return this.transport.send<OrganizationDetail>(
      'PATCH',
      `/api/platform/organizations/${organizationId}/status`,
      { status, reason }
    );
  }

  public updateLimits(organizationId: string, limits: CreateOrganizationRequest['limits']): Promise<OrganizationDetail> {
    return this.transport.send<OrganizationDetail>(
      'PATCH',
      `/api/platform/organizations/${organizationId}/limits`,
      { limits }
    );
  }

  public getHealth(organizationId: string): Promise<OrganizationHealth> {
    return this.transport.send<OrganizationHealth>('GET', `/api/platform/organizations/${organizationId}/health`);
  }

  public updateProfile(
    organizationId: string,
    request: { name: string; contactEmail: string | null; contactPhone: string | null; legalDetails: string | null }
  ): Promise<OrganizationDetail> {
    return this.transport.send<OrganizationDetail>(
      'PATCH',
      `/api/platform/organizations/${organizationId}`,
      request
    );
  }

  public updateUpdateChannel(
    organizationId: string,
    request: { channel: string; pinnedClientVersion: string | null }
  ): Promise<OrganizationDetail> {
    return this.transport.send<OrganizationDetail>(
      'PATCH',
      `/api/platform/organizations/${organizationId}/update-channel`,
      request
    );
  }

  public createBranch(organizationId: string, request: CreateBranchRequest): Promise<OrganizationBranch> {
    return this.transport.send<OrganizationBranch>(
      'POST',
      `/api/platform/organizations/${organizationId}/branches`,
      request
    );
  }

  public transferOwner(
    organizationId: string,
    request: { newOwnerEmail: string; reason: string }
  ): Promise<OrganizationOwnerInvite> {
    return this.transport.send<OrganizationOwnerInvite>(
      'POST',
      `/api/platform/organizations/${organizationId}/owner-transfer`,
      request
    );
  }
}
