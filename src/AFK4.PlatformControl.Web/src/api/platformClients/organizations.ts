import type { PlatformTransport } from '../platformTransport';
import type {
  CreateOrganizationRequest,
  CreateOrganizationResponse,
  OrganizationDetail,
  OrganizationHealth,
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
}
