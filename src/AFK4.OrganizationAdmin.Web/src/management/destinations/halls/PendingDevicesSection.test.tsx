import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import { permissionNames } from '../../../operatorPermissions';

const listPendingDevices = mock(async () => [{
  deviceId: '11111111-1111-1111-1111-111111111111',
  machineName: 'PC-NEW-07'
}] as never[]);
const approveDevice = mock(async () => ({}));
const rejectDevice = mock(async () => ({}));
const getBranchSettings = mock(async () => ({
  organizationId: 'org',
  branchId: 'b1',
  requireManualDeviceApproval: false,
  preferredLocale: 'tg'
}));
const updateBranchSettings = mock(async () => ({
  organizationId: 'org',
  branchId: 'b1',
  requireManualDeviceApproval: true,
  preferredLocale: 'tg'
}));

const actualHelpers = await import('../../../operatorHelpers');
mock.module('../../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { getBranchSettings, updateBranchSettings },
    devices: { listPendingDevices, approveDevice, rejectDevice }
  })
}));

const { PendingDevicesSection } = await import('./PendingDevicesSection');

afterAll(() => {
  mock.module('../../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  listPendingDevices.mockClear();
  approveDevice.mockClear();
  rejectDevice.mockClear();
  getBranchSettings.mockClear();
  updateBranchSettings.mockClear();
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: {
    accessToken: 't',
    organizationId: 'org',
    permissions: [
      permissionNames.assignDeviceSeat,
      permissionNames.viewDeviceDetail,
      permissionNames.manageBranchSettings
    ]
  },
  branchId: 'b1'
};

function renderSection(overrides: Record<string, unknown> = {}) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <PendingDevicesSection
          backend={backend as never}
          canViewDeviceDetail
          canApproveDevice
          canManageBranchSettings
          onApproved={mock(async () => {})}
          onFeedback={mock(() => {})}
          {...overrides}
        />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('PendingDevicesSection', () => {
  // Серверная очередь подтверждения работала с самого начала и не имела ни одного клиента:
  // ПК, поставленный на ручное подтверждение, висел бы в ожидании вечно.
  it('показывает ожидающий ПК и подтверждает его', async () => {
    renderSection();

    fireEvent.click(await screen.findByRole('button', { name: 'Подтвердить' }));

    await waitFor(() => expect(approveDevice).toHaveBeenCalledWith(
      '11111111-1111-1111-1111-111111111111',
      { organizationId: 'org' }
    ));
  });

  it('отклоняет ПК только после подтверждения и передаёт причину', async () => {
    renderSection();

    fireEvent.click(await screen.findByRole('button', { name: 'Отклонить' }));
    expect(rejectDevice).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText('Причина (необязательно)'), { target: { value: 'Не наш ПК' } });
    fireEvent.click(screen.getByRole('button', { name: 'Отклонить ПК' }));

    await waitFor(() => expect(rejectDevice).toHaveBeenCalledWith(
      '11111111-1111-1111-1111-111111111111',
      { organizationId: 'org', reason: 'Не наш ПК' }
    ));
  });

  it('без права решать показывает очередь, но не обещает действий', async () => {
    renderSection({ canApproveDevice: false });

    await screen.findByText('PC-NEW-07');
    expect(screen.queryByRole('button', { name: 'Подтвердить' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Отклонить' })).toBeNull();
  });

  // Запись настроек заменяется целиком. Если собрать её из одного переключателя, язык филиала
  // молча станет пустым — филиал с таджикским интерфейсом переедет на значение по умолчанию.
  it('включая ручное подтверждение, не стирает язык филиала', async () => {
    renderSection();
    await screen.findByText('PC-NEW-07');

    fireEvent.click(screen.getByRole('checkbox'));

    await waitFor(() => expect(updateBranchSettings).toHaveBeenCalledWith('b1', {
      organizationId: 'org',
      requireManualDeviceApproval: true,
      preferredLocale: 'tg'
    }));
  });

  it('без права на настройки филиала переключателя нет', async () => {
    renderSection({ canManageBranchSettings: false });

    await screen.findByText('PC-NEW-07');
    expect(screen.queryByRole('checkbox')).toBeNull();
  });

  // Сбой загрузки и пустая очередь — разные ответы. Показать первое как второе значит сказать
  // «никто не ждёт», ничего не зная, и новый ПК так и останется невидимым.
  it('не выдаёт сбой загрузки за пустую очередь', async () => {
    listPendingDevices.mockImplementationOnce(async () => { throw new Error('boom'); });
    renderSection();

    await screen.findByText('Не удалось загрузить очередь новых ПК.');
    expect(screen.queryByText('Никто не ждёт подтверждения.')).toBeNull();
  });
});
