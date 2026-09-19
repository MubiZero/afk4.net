import type { AdminsApi } from '@/api/platformClients/admins';
import type { PlatformAdminInvitation, PlatformAdminListItem } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

/// Люди платформы и приглашения, которые ещё не приняты: раздел показывает их вместе, и порознь
/// они бессмысленны — приглашение без списка не с чем сравнить.
export interface AdminsData {
  admins: PlatformAdminListItem[];
  invitations: PlatformAdminInvitation[];
}

export type AdminsState = Loadable<AdminsData>;

type Client = Pick<AdminsApi, 'listAdmins' | 'listInvitations'>;

export function useAdmins(client: Client): AdminsState {
  return useLoadable(async () => {
    const [admins, invitations] = await Promise.all([client.listAdmins(), client.listInvitations()]);
    return { admins, invitations };
  });
}
