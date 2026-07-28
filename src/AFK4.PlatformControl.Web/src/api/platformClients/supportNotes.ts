import type { PlatformTransport } from '../platformTransport';
import type { OrganizationSupportNote } from '../types';

export class SupportNotesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listSupportNotes(organizationId: string): Promise<OrganizationSupportNote[]> {
    return this.transport.send<OrganizationSupportNote[]>(
      'GET',
      `/api/platform/organizations/${organizationId}/support-notes`
    );
  }

  public createSupportNote(organizationId: string, body: string): Promise<OrganizationSupportNote> {
    return this.transport.send<OrganizationSupportNote>(
      'POST',
      `/api/platform/organizations/${organizationId}/support-notes`,
      { body }
    );
  }

  public updateSupportNote(
    organizationId: string,
    organizationSupportNoteId: string,
    body: string
  ): Promise<OrganizationSupportNote> {
    return this.transport.send<OrganizationSupportNote>(
      'PATCH',
      `/api/platform/organizations/${organizationId}/support-notes/${organizationSupportNoteId}`,
      { body }
    );
  }
}
