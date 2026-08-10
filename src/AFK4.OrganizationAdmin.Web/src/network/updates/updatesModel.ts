import type { MessageKey } from '@afk4/i18n';
import type { DeviceUpdateStatusSnapshotDto, UpdateRolloutStatusDto } from '../../api/clients/updates';

// Компонент раскатки, отвечающий за само приложение администратора. Раскатки клиентских ПК сюда
// не попадают намеренно: клуб может повлиять только на обновление своего рабочего места, всё
// остальное ведёт платформа.
export const ADMIN_COMPONENT = 'organization-admin';

// Статусы, из которых перезапуск «сейчас» имеет смысл: пакет уже предложен/скачан/ждёт выхода из
// приложения. В остальных (устанавливается, установлен, провалился) кнопка только вводит в
// заблуждение.
const RESTARTABLE_STATUSES = new Set(['offered', 'downloaded', 'deferred', 'ready-to-install', 'awaiting-app-exit']);

export function findAdminRollout(rollouts: readonly UpdateRolloutStatusDto[]): UpdateRolloutStatusDto | null {
  return rollouts.find((rollout) => rollout.component === ADMIN_COMPONENT) ?? null;
}

// Раскатка приходит со снимком по каждому устройству; администратора интересует самый свежий.
export function latestDeviceStatus(rollout: UpdateRolloutStatusDto | null): DeviceUpdateStatusSnapshotDto | null {
  if (rollout === null || rollout.deviceStatuses.length === 0) return null;
  return [...rollout.deviceStatuses].sort((left, right) => right.updatedAtUtc.localeCompare(left.updatedAtUtc))[0];
}

export function canRestartNow(status: DeviceUpdateStatusSnapshotDto | null): boolean {
  return status !== null && RESTARTABLE_STATUSES.has(status.status);
}

// Сообщение от службы обновления пишется для инженера и может нести ссылку на артефакт, хеш или
// имя пакета. Клубу это не нужно и знать не положено — такие строки заменяем нейтральной.
export function safeUpdateMessage(message: string | undefined | null): string | null {
  if (!message) return null;
  return /(https?:\/\/|sha-?256|signature|artifact|\.msi)/i.test(message) ? null : message;
}

const STATUS_LABELS: Record<string, MessageKey> = {
  offered: 'op.network.updates.status.offered',
  downloaded: 'op.network.updates.status.downloaded',
  deferred: 'op.network.updates.status.deferred',
  'ready-to-install': 'op.network.updates.status.readyToInstall',
  'awaiting-app-exit': 'op.network.updates.status.awaitingAppExit',
  installing: 'op.network.updates.status.installing',
  installed: 'op.network.updates.status.installed',
  failed: 'op.network.updates.status.failed'
};

export function updateStatusLabelKey(status: string): MessageKey | null {
  return STATUS_LABELS[status] ?? null;
}

export function updateStatusTone(status: string): string {
  if (status === 'failed') return 'ui-chip--danger';
  if (status === 'installed') return 'ui-chip--ok';
  return 'ui-chip--attention';
}

// Окно обслуживания хранится как время с секундами («02:00:00»), а <input type="time"> работает с
// «02:00». Пустая строка означает «поле ещё не заполнено», а не полночь.
export function toTimeInput(value: string | undefined | null): string {
  return value ? value.slice(0, 5) : '';
}

export function toTimeRequest(value: string): string {
  return `${value}:00`;
}

// Окно с совпадающими краями сервер отвергает (нулевая длительность), поэтому не даём его отправить.
export function isWindowValid(start: string, end: string): boolean {
  return start !== '' && end !== '' && start !== end;
}
