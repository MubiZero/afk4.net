import type { PlatformTransport } from '../platformTransport';
import type {
  CreateTenantRequest,
  CreateTenantResponse,
  TenantDetail,
  TenantHealth,
  TenantSummary
} from '../types';

export class TenantsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listTenants(): Promise<TenantSummary[]> {
    return this.transport.send<TenantSummary[]>('GET', '/api/platform/tenants');
  }

  public getTenant(organizationId: string): Promise<TenantDetail> {
    return this.transport.send<TenantDetail>('GET', `/api/platform/tenants/${organizationId}`);
  }

  public createTenant(request: CreateTenantRequest): Promise<CreateTenantResponse> {
    return this.transport.send<CreateTenantResponse>('POST', '/api/platform/tenants', request);
  }

  public updateStatus(organizationId: string, status: string, reason: string): Promise<TenantDetail> {
    return this.transport.send<TenantDetail>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/status`,
      { status, reason }
    );
  }

  public updateLimits(organizationId: string, limits: CreateTenantRequest['limits']): Promise<TenantDetail> {
    return this.transport.send<TenantDetail>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/limits`,
      { limits }
    );
  }

  public getHealth(organizationId: string): Promise<TenantHealth> {
    return this.transport.send<TenantHealth>('GET', `/api/platform/tenants/${organizationId}/health`);
  }
}
