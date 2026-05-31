// src/club/venue/VenueScreen.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { VenueScreen } from './VenueScreen';
import type { DeviceInventoryItem, FloorMap } from '@/api/types';

const floorMap: FloorMap = { branchId: 'b1', branchName: 'X', zones: [], seats: [] };
function device(over: Partial<DeviceInventoryItem>): DeviceInventoryItem {
  return {
    organizationId: 'org', branchId: 'b1', deviceId: 'd1', machineName: 'PC', agentVersion: '1', shellVersion: '1',
    enrolledAtUtc: '2026-05-01T00:00:00Z', lastHeartbeatAtUtc: null, isOnline: true, isLocked: false,
    seatId: null, seatName: null, zoneId: null, zoneName: null, activeCredentialCount: 0, installedAppCount: 0,
    pendingCommandCount: 0, failedCommandCount: 0, displayName: 'ПК-1', role: 'workstation', enrollmentState: 'active', ...over
  };
}
function client() {
  return {
    listDevices: mock().mockResolvedValue([device({ deviceId: 'd1', displayName: 'ПК-1' })]),
    listPendingDevices: mock().mockResolvedValue([]),
    getFloorMap: mock().mockResolvedValue({ etag: null, floorMap }),
    updateFloorMap: mock(async () => ({ eTag: 'e2', zones: [], seats: [] }))
  } as never;
}

it('renders the devices table and opens the drawer on row click', async () => {
  render(<I18nProvider><ToastProvider><VenueScreen client={client()} branchId="b1" organizationId="org" canManageLayout /></ToastProvider></I18nProvider>);
  await waitFor(() => expect(screen.getByText('ПК-1')).toBeInTheDocument());
  fireEvent.click(screen.getByText('ПК-1'));
  expect(screen.getByLabelText('Название')).toBeInTheDocument(); // drawer body
});

it('renders the floor-map editor in the map tab', async () => {
  render(<I18nProvider><ToastProvider><VenueScreen client={client()} branchId="b1" organizationId="org" canManageLayout /></ToastProvider></I18nProvider>);
  const mapTab = await screen.findByRole('tab', { name: 'Карта зала' });
  fireEvent.mouseDown(mapTab);
  fireEvent.click(mapTab);
  expect(await screen.findByRole('button', { name: 'Добавить зону' })).toBeInTheDocument();
});
