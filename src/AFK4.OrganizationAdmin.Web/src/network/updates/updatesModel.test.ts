import { describe, expect, it } from 'bun:test';
import {
  canRestartNow,
  findAdminRollout,
  isWindowValid,
  latestDeviceStatus,
  safeUpdateMessage,
  toTimeInput,
  toTimeRequest,
  updateStatusTone
} from './updatesModel';
import type { DeviceUpdateStatusSnapshotDto, UpdateRolloutStatusDto } from '../../api/clients/updates';

function rollout(component: string, statuses: Partial<DeviceUpdateStatusSnapshotDto>[] = []): UpdateRolloutStatusDto {
  return {
    updateRolloutId: `rollout-${component}`,
    organizationId: 'org',
    branchId: 'branch',
    updatePackageId: 'package',
    component,
    version: '1.0.0',
    channel: 'stable',
    state: 'active',
    startsAtUtc: '2026-08-01T00:00:00Z',
    deviceStatuses: statuses.map((status, index) => ({
      deviceId: `device-${index}`,
      updateRolloutId: `rollout-${component}`,
      updatePackageId: 'package',
      component,
      installedVersion: '0.9.0',
      targetVersion: '1.0.0',
      status: 'offered',
      message: '',
      updatedAtUtc: '2026-08-01T00:00:00Z',
      ...status
    }))
  };
}

describe('updatesModel', () => {
  it('берёт раскатку приложения администратора, а не первую попавшуюся', () => {
    const found = findAdminRollout([rollout('player-shell'), rollout('organization-admin'), rollout('agent')]);
    expect(found?.component).toBe('organization-admin');
  });

  it('без раскатки администратора возвращает null, а не чужой компонент', () => {
    expect(findAdminRollout([rollout('player-shell')])).toBeNull();
  });

  it('из снимков устройств берёт самый свежий', () => {
    const status = latestDeviceStatus(rollout('organization-admin', [
      { updatedAtUtc: '2026-08-01T00:00:00Z', installedVersion: 'старая' },
      { updatedAtUtc: '2026-08-09T00:00:00Z', installedVersion: 'свежая' }
    ]));
    expect(status?.installedVersion).toBe('свежая');
  });

  it('перезапуск предлагает только когда пакет ждёт установки', () => {
    const ready = latestDeviceStatus(rollout('organization-admin', [{ status: 'downloaded' }]));
    const installing = latestDeviceStatus(rollout('organization-admin', [{ status: 'installing' }]));
    const done = latestDeviceStatus(rollout('organization-admin', [{ status: 'installed' }]));

    expect(canRestartNow(ready)).toBe(true);
    expect(canRestartNow(installing)).toBe(false);
    expect(canRestartNow(done)).toBe(false);
    expect(canRestartNow(null)).toBe(false);
  });

  // Служебное сообщение может нести ссылку, хеш или имя пакета — клубу это показывать незачем.
  it('прячет технические сообщения и оставляет человеческие', () => {
    expect(safeUpdateMessage('Пакет загружен')).toBe('Пакет загружен');
    expect(safeUpdateMessage('download from https://cdn.example/pkg.msi')).toBeNull();
    expect(safeUpdateMessage('sha256 mismatch')).toBeNull();
    expect(safeUpdateMessage('signature check failed')).toBeNull();
    expect(safeUpdateMessage('')).toBeNull();
    expect(safeUpdateMessage(null)).toBeNull();
  });

  it('переводит время между полем и запросом', () => {
    expect(toTimeInput('03:00:00')).toBe('03:00');
    expect(toTimeInput(null)).toBe('');
    expect(toTimeRequest('03:00')).toBe('03:00:00');
  });

  it('окно нулевой длины считает недопустимым', () => {
    expect(isWindowValid('02:00', '05:00')).toBe(true);
    expect(isWindowValid('02:00', '02:00')).toBe(false);
    expect(isWindowValid('', '05:00')).toBe(false);
  });

  it('провал установки помечает тревожным тоном, успех — спокойным', () => {
    expect(updateStatusTone('failed')).toBe('ui-chip--danger');
    expect(updateStatusTone('installed')).toBe('ui-chip--ok');
    expect(updateStatusTone('downloaded')).toBe('ui-chip--attention');
  });
});
