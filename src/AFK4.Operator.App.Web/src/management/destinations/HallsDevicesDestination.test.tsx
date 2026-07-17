import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { DeviceInventoryItemDto, ZoneDto } from '../../operatorApiClients';
import { HallsDevicesDestination } from './HallsDevicesDestination';

afterEach(() => cleanup());

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const zones: ZoneDto[] = [{ zoneId: 'z1', name: 'Зал VIP', sortOrder: 10, seats: [{ seatId: 's1', name: 'PC-01', sortOrder: 10 }] } as never];
const deviceInventory: DeviceInventoryItemDto[] = [{
  deviceId: 'd1',
  machineName: 'PC-VIP-01',
  zoneName: 'Зал VIP',
  seatName: 'PC-01',
  seatId: 's1',
  isOnline: true,
  isLocked: false,
  agentVersion: '1.2.3',
  installedAppCount: 4,
  pendingCommandCount: 0,
  failedCommandCount: 0
} as never];

describe('HallsDevicesDestination', () => {
  it('renders the ManagementScreen title and subtitle', () => {
    wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={[]}
        branchDeviceCommandHistory={[]}
      />
    );

    expect(screen.getByRole('heading', { name: 'Залы и ПК' })).toBeTruthy();
    expect(screen.getByText('Залы, зоны и рабочие места')).toBeTruthy();
  });

  it('defaults to the "Залы и места" tab and shows the zones from state', () => {
    wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={deviceInventory}
        branchDeviceCommandHistory={[]}
      />
    );

    expect(screen.getByRole('tab', { name: 'Залы и места', selected: true })).toBeTruthy();
    expect(screen.getByText('Зал VIP')).toBeTruthy();
  });

  it('switches to the devices tab and shows device inventory instead of zones', () => {
    wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={deviceInventory}
        branchDeviceCommandHistory={[]}
      />
    );

    fireEvent.click(screen.getByRole('tab', { name: 'Устройства' }));
    expect(screen.queryByText('Зал VIP')).toBeNull();
    expect(screen.getByText('PC-VIP-01')).toBeTruthy();
  });

  it('calls onDirtyChange(false) on mount since the section saves per-action', () => {
    const onDirtyChange = mock(() => {});
    wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={[]}
        branchDeviceCommandHistory={[]}
        onDirtyChange={onDirtyChange}
      />
    );
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });

  it('shows a loading skeleton instead of the tabs while loadStatus is loading', () => {
    const { container } = wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={[]}
        branchDeviceCommandHistory={[]}
        loadStatus="loading"
      />
    );
    expect(container.querySelector('.management-skeleton')).toBeTruthy();
    expect(screen.queryByText('Зал VIP')).toBeNull();
  });

  it('shows the concrete error detail and retries via onRetry when loadStatus is failed', () => {
    const onRetry = mock(() => {});
    wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={[]}
        branchDeviceCommandHistory={[]}
        loadStatus="failed"
        errorDetail="boom"
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('boom')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
