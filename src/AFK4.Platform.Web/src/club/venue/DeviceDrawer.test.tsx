// src/club/venue/DeviceDrawer.test.tsx
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
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
    renameDevice: mock().mockResolvedValue({}),
    moveDeviceSeat: mock().mockResolvedValue({}),
    removeDevice: mock().mockResolvedValue({}),
    approveDevice: mock().mockResolvedValue({}),
    rejectDevice: mock().mockResolvedValue({})
  };
}

function renderDrawer(row: DeviceRow, client = fakeClient(), onDone = mock()) {
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

it('moves device to current seat when move-seat button is clicked', async () => {
  // activeRow.seatId is 's1', so seatId state initialises to 's1' and button is enabled
  const { client } = renderDrawer(activeRow);
  fireEvent.click(screen.getByRole('button', { name: 'Переместить на место' }));
  await waitFor(() => expect(client.moveDeviceSeat).toHaveBeenCalledWith('d1', 'org', 's1'));
});

it('rejects a pending device through the confirm dialog', async () => {
  const { client } = renderDrawer({ ...activeRow, status: 'pending' });
  // Click the trigger button to open the confirm dialog
  fireEvent.click(screen.getByRole('button', { name: 'Отклонить' }));
  // Wait for the dialog to appear, then click the confirm button scoped inside it
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.click(within(dialog).getByRole('button', { name: 'Отклонить' }));
  await waitFor(() => expect(client.rejectDevice).toHaveBeenCalledWith('d1', 'org', expect.any(String)));
});

it('shows error toast and does not call onDone when renameDevice fails', async () => {
  const errorClient = {
    ...fakeClient(),
    renameDevice: mock().mockRejectedValue(new Error('boom')),
  };
  const { onDone } = renderDrawer(activeRow, errorClient as never);
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'ПК-bad' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(screen.getByText('Не удалось выполнить действие')).toBeInTheDocument());
  expect(onDone).not.toHaveBeenCalled();
});
