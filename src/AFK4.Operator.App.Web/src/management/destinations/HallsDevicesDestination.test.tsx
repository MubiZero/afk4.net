import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { permissionNames } from '../../operatorPermissions';
import type { ZoneDto } from '../../operatorApiClients';
import { HallsDevicesDestination } from './HallsDevicesDestination';

afterEach(() => cleanup());

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const zones: ZoneDto[] = [{ zoneId: 'z1', name: 'Зал VIP', sortOrder: 10, seats: [] } as never];

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

  it('renders the zones passed in from ManagementWorkspace state', () => {
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

    expect(screen.getByRole('button', { name: /Зал VIP/ })).toBeTruthy();
  });

  it('derives can* flags from session permissions, not just backend presence', () => {
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
    // No manageLayout permission -> create-zone action disabled.
    expect(screen.getByRole('button', { name: 'Создать зал' })).toBeDisabled();
    cleanup();

    wrap(
      <HallsDevicesDestination
        backend={null}
        session={session([permissionNames.manageLayout])}
        currencyCode="TJS"
        zones={zones}
        deviceInventory={[]}
        branchDeviceCommandHistory={[]}
      />
    );
    expect(screen.getByRole('button', { name: 'Создать зал' })).not.toBeDisabled();
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
});
