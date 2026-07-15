import type { MessageKey } from '@afk4/i18n';
import type { TFunc } from './operatorHelpers';
import type { OperatorRealtimeConnectionState } from './operatorRealtime';

export type SystemStatusTone = 'neutral' | 'ok' | 'warn' | 'bad';

export interface SystemStatusField {
  key: 'operator' | 'role' | 'club';
  label: string;
  value: string;
}

export interface SystemStatusInput {
  operatorName: string;
  roleNames: string[];
  clubName: string;
  realtimeState: OperatorRealtimeConnectionState;
  dataSource: string;
  appVersion: string;
}

export interface SystemStatusValue {
  value: string;
  tone: SystemStatusTone;
}

export interface SystemStatusViewModel {
  left: SystemStatusField[];
  connection: SystemStatusValue;
  server: SystemStatusValue;
  version: SystemStatusValue;
}

export function staffRoleLabel(roleName: string, t: TFunc): string {
  const key = `roles.${roleName}` as MessageKey;
  const translated = t(key);
  return translated === key ? roleName : translated;
}

export function buildSystemStatusModel(input: SystemStatusInput, t: TFunc): SystemStatusViewModel {
  const displayValue = (candidate: string | null | undefined) => candidate?.trim() || '—';
  const connection: SystemStatusValue = input.realtimeState === 'connected'
    ? { value: t('op.status.online'), tone: 'ok' }
    : input.realtimeState === 'connecting' || input.realtimeState === 'reconnecting'
      ? { value: t('op.status.reconnecting'), tone: 'warn' }
      : { value: t('op.status.offline'), tone: 'bad' };

  return {
    left: [
      { key: 'operator', label: t('op.status.operator'), value: displayValue(input.operatorName) },
      {
        key: 'role',
        label: t('op.status.role'),
        value: input.roleNames.length > 0
          ? input.roleNames.map((role) => staffRoleLabel(role, t)).join(', ')
          : '—'
      },
      { key: 'club', label: t('op.status.club'), value: displayValue(input.clubName) }
    ],
    connection,
    server: input.dataSource === 'backend'
      ? { value: t('op.status.serverOk'), tone: 'ok' }
      : { value: t('op.status.serverUnavailable'), tone: 'bad' },
    version: { value: displayValue(input.appVersion), tone: 'neutral' }
  };
}
