import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { DevicesTable } from './DevicesTable';
import type { DeviceRow } from './devicesModel';

const row: DeviceRow = {
  deviceId: 'd1', organizationId: 'org', displayName: 'ПК-1', machineName: 'PC-RAW',
  seatId: 's1', seatLabel: 'Зона A · Место 1', status: 'online', lastHeartbeatAtUtc: null, failedCommandCount: 0
};

function renderTable(rows: DeviceRow[], onSelect = mock()) {
  render(<I18nProvider><DevicesTable rows={rows} emptyMessage="Нет устройств" onSelect={onSelect} /></I18nProvider>);
  return onSelect;
}

it('renders rows and fires onSelect on row click', () => {
  const onSelect = renderTable([row]);
  fireEvent.click(screen.getByText('ПК-1'));
  expect(onSelect).toHaveBeenCalledWith(row);
});

it('renders the empty message when there are no rows', () => {
  renderTable([]);
  expect(screen.getByText('Нет устройств')).toBeInTheDocument();
});
