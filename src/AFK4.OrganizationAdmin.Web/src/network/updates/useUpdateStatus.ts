import { useRef } from 'react';
import type { OrganizationAdminUpdatePreferenceDto, UpdateRolloutStatusDto } from '../../api/clients/updates';
import { useSection, type Section } from '../useSection';

export interface UpdateStatusClient {
  getRolloutStatuses(branchId: string): Promise<UpdateRolloutStatusDto[]>;
  getPreference(branchId: string): Promise<OrganizationAdminUpdatePreferenceDto>;
  updatePreference(
    branchId: string,
    request: Pick<OrganizationAdminUpdatePreferenceDto, 'organizationId' | 'maintenanceWindowStart' | 'maintenanceWindowEnd'>
  ): Promise<OrganizationAdminUpdatePreferenceDto>;
}

export interface UpdateStatusState {
  rollouts: Section<UpdateRolloutStatusDto[]>;
  // Сохранение окна возвращает свежую запись — её кладут в секцию через `apply`, вместо повторной
  // загрузки экрана.
  preference: Section<OrganizationAdminUpdatePreferenceDto>;
}

// Раскатки и окно обслуживания — две независимые панели экрана: версию и «перезапустить сейчас»
// можно показать без окна, а окно настроить без статуса. Раньше они грузились одним ожиданием, и
// отказ любого стирал оба; теперь каждая панель живёт своей загрузкой и своим повтором.
export function useUpdateStatus(client: UpdateStatusClient, branchId: string): UpdateStatusState {
  const clientRef = useRef(client);
  clientRef.current = client;
  const rollouts = useSection(async () => {
    const next = await clientRef.current.getRolloutStatuses(branchId);
    return Array.isArray(next) ? next : [];
  }, branchId);
  const preference = useSection(() => clientRef.current.getPreference(branchId), branchId);
  return { rollouts, preference };
}
