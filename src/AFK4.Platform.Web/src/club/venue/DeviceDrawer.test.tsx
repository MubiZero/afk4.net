// src/club/venue/DeviceDrawer.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { DeviceDrawer } from './DeviceDrawer';
import type { DeviceRow, SeatOption } from './devicesModel';

const activeRow: DeviceRow = {
  deviceId: 'd1', organizationId: 'org', displayName: 'ПК-1', machineName: 'PC-RAW',
  seatId: 's1', seatLabel: 'Зона A · Место 1', status: 'online', lastHeartbeatAtUtc: null, failedCommandCount: 0
};
const seatOptions: SeatOption[] = [{ seatId: 's2', label: 'Зона B · Место 2' }];

function fakeClient() {
  return {
    renameDevice: vi.fn().mockResolvedValue({}),
    moveDeviceSeat: vi.fn().mockResolvedValue({}),
    removeDevice: vi.fn().mockResolvedValue({}),
    approveDevice: vi.fn().mockResolvedValue({}),
    rejectDevice: vi.fn().mockResolvedValue({})
  };
}

function renderDrawer(row: DeviceRow, client = fakeClient(), onDone = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <DeviceDrawer device={row} seatOptions={seatOptions} client={client as never} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('renames a device and calls onDone', async () => {
  const { client, onDone } = renderDrawer(activeRow);
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'ПК-новый' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.renameDevice).toHaveBeenCalledWith('d1', 'org', 'ПК-новый'));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('removes a device through the confirm dialog', async () => {
  const { client } = renderDrawer(activeRow);
  fireEvent.click(screen.getByRole('button', { name: 'Удалить устройство' }));
  fireEvent.click(screen.getByRole('button', { name: 'Удалить' })); // confirm
  await waitFor(() => expect(client.removeDevice).toHaveBeenCalled());
});

it('approves a pending device', async () => {
  const { client } = renderDrawer({ ...activeRow, status: 'pending' });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.approveDevice).toHaveBeenCalledWith('d1', 'org', expect.any(String)));
});
