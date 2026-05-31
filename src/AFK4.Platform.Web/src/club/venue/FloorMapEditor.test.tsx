import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformApi';
import type { FloorMap } from '@/api/types';
import { FloorMapEditor } from './FloorMapEditor';

function floorMap(): FloorMap {
  return {
    branchId: 'b1', branchName: 'Центр',
    zones: [{ zoneId: 'z1', name: 'Зона A', sortOrder: 1 }],
    seats: [{ seatId: 's1', seatName: 'ПК-1', zoneId: 'z1', zoneName: 'Зона A', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }]
  };
}

function fakeClient(overrides: Record<string, unknown> = {}) {
  return {
    getFloorMap: mock(async () => ({ etag: 'etag-1', floorMap: floorMap() })),
    updateFloorMap: mock(async () => ({ eTag: 'etag-2', zones: [], seats: [] })),
    ...overrides
  };
}

function setup(client = fakeClient(), canEdit = true) {
  render(
    <I18nProvider><ToastProvider>
      <FloorMapEditor client={client as never} branchId="b1" organizationId="org" canEdit={canEdit} />
    </ToastProvider></I18nProvider>
  );
  return { client };
}

it('renders zones and seats from the loaded map', async () => {
  setup();
  expect(await screen.findByDisplayValue('Зона A')).toBeInTheDocument();
  expect(screen.getByDisplayValue('ПК-1')).toBeInTheDocument();
});

it('adds a zone', async () => {
  setup();
  await screen.findByDisplayValue('Зона A');
  fireEvent.click(screen.getByRole('button', { name: 'Добавить зону' }));
  await waitFor(() => expect(screen.getAllByLabelText('Название зоны').length).toBe(2));
});

it('saves via updateFloorMap and toasts success', async () => {
  const { client } = setup();
  await screen.findByDisplayValue('Зона A');
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить карту' }));
  await waitFor(() => expect(client.updateFloorMap).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org' }), 'etag-1'));
});

it('shows the conflict banner on a 412', async () => {
  const client = fakeClient({ updateFloorMap: mock(async () => { throw new PlatformApiError(412, 'stale'); }) });
  setup(client);
  await screen.findByDisplayValue('Зона A');
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить карту' }));
  expect(await screen.findByText('Карта изменилась в другом сеансе. Перезагрузите перед сохранением.')).toBeInTheDocument();
});

it('is read-only without the edit permission', async () => {
  setup(fakeClient(), false);
  await screen.findByDisplayValue('Зона A');
  expect(screen.queryByRole('button', { name: 'Сохранить карту' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Добавить зону' })).not.toBeInTheDocument();
  expect(screen.getByDisplayValue('Зона A')).toBeDisabled();
});
