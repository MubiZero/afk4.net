import type { PlatformTransport } from '../platformTransport';
import type { PlatformRole } from '../types';

export interface SavePlatformRoleRequest {
  displayName: string;
  description: string;
  permissions: string[];
}

export class RolesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listRoles(): Promise<PlatformRole[]> {
    return this.transport.send<PlatformRole[]>('GET', '/api/platform/roles');
  }

  public listPermissions(): Promise<string[]> {
    return this.transport.send<string[]>('GET', '/api/platform/permissions');
  }

  public createRole(roleName: string, request: SavePlatformRoleRequest): Promise<PlatformRole> {
    return this.transport.send<PlatformRole>('POST', '/api/platform/roles', { roleName, ...request });
  }

  public updateRole(roleName: string, request: SavePlatformRoleRequest): Promise<PlatformRole> {
    return this.transport.send<PlatformRole>('PUT', `/api/platform/roles/${encodeURIComponent(roleName)}`, request);
  }

  public async deleteRole(roleName: string): Promise<void> {
    await this.transport.send<void>('DELETE', `/api/platform/roles/${encodeURIComponent(roleName)}`);
  }
}
