import type { AdminsApi } from '@/api/platformClients/admins';
import type { PlatformAdminInvitation, PlatformAdminListItem } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

/// Люди платформы и приглашения, которые ещё не приняты. Раздел показывает их одной таблицей,
/// но грузит порознь: отказ приглашений не должен прятать список сотрудников — им и без
/// приглашений можно управлять, — а повтор должен перезапрашивать только то, что не пришло.
export interface AdminsState {
  admins: Loadable<PlatformAdminListItem[]>;
  invitations: Loadable<PlatformAdminInvitation[]>;
}

type Client = Pick<AdminsApi, 'listAdmins' | 'listInvitations'>;

export function useAdmins(client: Client): AdminsState {
  const admins = useLoadable(() => client.listAdmins());
  const invitations = useLoadable(() => client.listInvitations());
  return { admins, invitations };
}
