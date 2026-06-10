import type { PlatformTransport } from '../platformTransport';
import type { TenantSupportNote } from '../types';

export class SupportNotesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listSupportNotes(organizationId: string): Promise<TenantSupportNote[]> {
    return this.transport.send<TenantSupportNote[]>(
      'GET',
      `/api/platform/tenants/${organizationId}/support-notes`
    );
  }

  public createSupportNote(organizationId: string, body: string): Promise<TenantSupportNote> {
    return this.transport.send<TenantSupportNote>(
      'POST',
      `/api/platform/tenants/${organizationId}/support-notes`,
      { body }
    );
  }

  public updateSupportNote(
    organizationId: string,
    tenantSupportNoteId: string,
    body: string
  ): Promise<TenantSupportNote> {
    return this.transport.send<TenantSupportNote>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/support-notes/${tenantSupportNoteId}`,
      { body }
    );
  }
}
