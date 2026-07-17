import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import { permissionNames } from '../../../operatorPermissions';
import type { DeviceInventoryItemDto } from '../../../operatorApiClients';

const getDeviceDetail = mock(async () => ({
  machineName: 'PC-VIP-01',
  isOnline: true,
  isLocked: false,
  zoneName: 'Зал VIP',
  seatName: 'PC-01',
  lastHeartbeatAtUtc: '2026-07-16T10:00:00Z',
  agentVersion: '1.2.3',
  shellVersion: '1.0.0',
  activeCredentialCount: 1,
  installedAppCount: 4
}));
const listDeviceCommands = mock(async () => []);
const listBranchDeviceCommands = mock(async () => []);
const listDevices = mock(async () => []);
const dispatchDeviceCommand = mock(async () => ({ type: 'lock' }));
const rotateDeviceCredential = mock(async () => ({ credentialId: '33333333-3333-3333-3333-333333333333', credentialSecret: 'top-secret' }));
const revokeDeviceCredential = mock(async () => undefined);
const createEnrollmentCode = mock(async () => ({ code: 'ABC123' }));
const assignDeviceSeat = mock(async () => ({}));

const actualHelpers = await import('../../../operatorHelpers');
mock.module('../../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { assignDeviceSeat },
    devices: {
      getDeviceDetail,
      listDeviceCommands,
      listBranchDeviceCommands,
      listDevices,
      dispatchDeviceCommand,
      rotateDeviceCredential,
      revokeDeviceCredential,
      createEnrollmentCode
    }
  })
}));

const { DevicesTab } = await import('./DevicesTab');

afterAll(() => {
  mock.module('../../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  getDeviceDetail.mockClear();
  listDeviceCommands.mockClear();
  listBranchDeviceCommands.mockClear();
  listDevices.mockClear();
  dispatchDeviceCommand.mockClear();
  rotateDeviceCredential.mockClear();
  revokeDeviceCredential.mockClear();
  createEnrollmentCode.mockClear();
  assignDeviceSeat.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: {
    accessToken: 't',
    organizationId: 'org',
    permissions: [
      permissionNames.createDeviceEnrollmentCode,
      permissionNames.assignDeviceSeat,
      permissionNames.viewDeviceDetail,
      permissionNames.viewDeviceCommandStatus,
      permissionNames.dispatchDeviceCommand,
      permissionNames.rotateDeviceCredential,
      permissionNames.revokeDeviceCredential
    ]
  },
  branchId: 'b1'
};

const deviceInventory: DeviceInventoryItemDto[] = [{
  deviceId: '11111111-1111-1111-1111-111111111111',
  machineName: 'PC-VIP-01',
  zoneName: 'Зал VIP',
  seatName: 'PC-01',
  seatId: '22222222-2222-2222-2222-222222222222',
  isOnline: true,
  isLocked: false,
  agentVersion: '1.2.3',
  installedAppCount: 4,
  pendingCommandCount: 0,
  failedCommandCount: 0,
  lastHeartbeatAtUtc: '2026-07-16T10:00:00Z'
} as never];

const layoutSeatOptions = [{ seatId: '22222222-2222-2222-2222-222222222222', label: 'Зал VIP · PC-01' }];

const baseProps = {
  deviceInventory,
  branchDeviceCommandHistory: [],
  layoutSeatOptions,
  onDeviceInventoryChange: mock(() => {}),
  onBranchDeviceCommandHistoryChange: mock(() => {}),
  onReload: mock(async () => {}),
  onFeedback: mock(() => {})
};

describe('DevicesTab', () => {
  it('renders the device inventory list', () => {
    wrap(
      <DevicesTab
        {...baseProps}
        backend={null}
        canCreateDeviceEnrollmentCode={false}
        canAssignDeviceSeat={false}
        canViewDeviceDetail={false}
        canViewDeviceCommandStatus={false}
        canDispatchDeviceCommand={false}
        canRotateDeviceCredential={false}
        canRevokeDeviceCredential={false}
      />
    );
    expect(screen.getByText('PC-VIP-01')).toBeTruthy();
  });

  it('opens the drawer and loads the device card on row click', async () => {
    wrap(
      <DevicesTab
        {...baseProps}
        backend={backend as never}
        canCreateDeviceEnrollmentCode
        canAssignDeviceSeat
        canViewDeviceDetail
        canViewDeviceCommandStatus
        canDispatchDeviceCommand
        canRotateDeviceCredential
        canRevokeDeviceCredential
      />
    );
    fireEvent.click(screen.getByText('PC-VIP-01'));
    await waitFor(() => expect(getDeviceDetail).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111'));
    await waitFor(() => expect(screen.getByText('1.2.3')).toBeTruthy());
  });

  it('hides "+ Подключить устройство" and drawer sections without the matching permissions', () => {
    wrap(
      <DevicesTab
        {...baseProps}
        backend={backend as never}
        canCreateDeviceEnrollmentCode={false}
        canAssignDeviceSeat={false}
        canViewDeviceDetail={false}
        canViewDeviceCommandStatus={false}
        canDispatchDeviceCommand={false}
        canRotateDeviceCredential={false}
        canRevokeDeviceCredential={false}
      />
    );
    expect(screen.queryByRole('button', { name: '+ Подключить устройство' })).toBeNull();
    fireEvent.click(screen.getByText('PC-VIP-01'));
    expect(screen.queryByText('Назначение')).toBeNull();
    expect(screen.queryByText('Ключи')).toBeNull();
    expect(getDeviceDetail).not.toHaveBeenCalled();
  });

  it('"+ Подключить устройство" opens the enrollment modal and creates a code', async () => {
    wrap(
      <DevicesTab
        {...baseProps}
        backend={backend as never}
        canCreateDeviceEnrollmentCode
        canAssignDeviceSeat={false}
        canViewDeviceDetail={false}
        canViewDeviceCommandStatus={false}
        canDispatchDeviceCommand={false}
        canRotateDeviceCredential={false}
        canRevokeDeviceCredential={false}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Подключить устройство' }));
    expect(screen.getByRole('dialog', { name: 'Подключить устройство' })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Создать код подключения' }));
    await waitFor(() => expect(createEnrollmentCode).toHaveBeenCalledWith('b1', 'org', 15 * 60));
    expect(await screen.findByDisplayValue('ABC123')).toBeTruthy();
  });

  it('assigns a device to a seat from the drawer', async () => {
    wrap(
      <DevicesTab
        {...baseProps}
        backend={backend as never}
        canCreateDeviceEnrollmentCode={false}
        canAssignDeviceSeat
        canViewDeviceDetail={false}
        canViewDeviceCommandStatus={false}
        canDispatchDeviceCommand={false}
        canRotateDeviceCredential={false}
        canRevokeDeviceCredential={false}
      />
    );
    fireEvent.click(screen.getByText('PC-VIP-01'));
    fireEvent.click(screen.getByRole('button', { name: 'Назначить устройство' }));
    await waitFor(() => expect(assignDeviceSeat).toHaveBeenCalledWith('b1', '11111111-1111-1111-1111-111111111111', { organizationId: 'org', seatId: '22222222-2222-2222-2222-222222222222' }));
  });

  it('rotates then revokes a device credential', async () => {
    wrap(
      <DevicesTab
        {...baseProps}
        backend={backend as never}
        canCreateDeviceEnrollmentCode={false}
        canAssignDeviceSeat={false}
        canViewDeviceDetail={false}
        canViewDeviceCommandStatus={false}
        canDispatchDeviceCommand={false}
        canRotateDeviceCredential
        canRevokeDeviceCredential
      />
    );
    fireEvent.click(screen.getByText('PC-VIP-01'));
    // До ротации отзыв недоступен — нет ключа, ждущего отзыва.
    expect(screen.getByRole('button', { name: 'Отозвать ключ' })).toBeDisabled();

    fireEvent.click(screen.getByRole('button', { name: 'Выдать новый ключ' }));
    await waitFor(() => expect(rotateDeviceCredential).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111'));
    expect(await screen.findByDisplayValue('top-secret')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Отозвать ключ' }));
    const confirmDialog = screen.getByRole('alertdialog');
    fireEvent.click(within(confirmDialog).getByRole('button', { name: 'Отозвать ключ' }));
    await waitFor(() => expect(revokeDeviceCredential).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111', '33333333-3333-3333-3333-333333333333'));
  });
});
