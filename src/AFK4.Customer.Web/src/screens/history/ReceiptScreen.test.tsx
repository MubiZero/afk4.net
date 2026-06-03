import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { ReceiptScreen } from './ReceiptScreen';
import { PlayerApiError } from '@/api/playerApi';
import type { PlayerApiClient } from '@/api/playerApi';

it('renders the receipt with its POS lines and grand total', async () => {
  const api = { getVisitReceipt: mock().mockResolvedValue({
    receiptNumber: 'R-1001', createdAtUtc: '2026-06-01T12:00:00Z', sessionId: 's1', seatName: 'PC-14',
    startedAtUtc: '2026-06-01T10:00:00Z', endedAtUtc: '2026-06-01T12:00:00Z', timeChargeMinorUnits: 12000,
    posLines: [{ productName: 'Кола', quantity: 2, unitPriceMinorUnits: 1500, lineTotalMinorUnits: 3000 }],
    posTotalMinorUnits: 3000, grandTotalMinorUnits: 15000, currencyCode: 'TJS'
  }) } as unknown as PlayerApiClient;
  render(<ReceiptScreen api={api} sessionId="s1" onBack={() => {}} />);
  expect(await screen.findByText('R-1001')).toBeInTheDocument();
  expect(screen.getByText('Кола')).toBeInTheDocument();
  expect(screen.getByText('150,00 TJS')).toBeInTheDocument();
});

it('renders a not-found state when the receipt is foreign or missing (404)', async () => {
  const api = { getVisitReceipt: mock().mockRejectedValue(new PlayerApiError(404, 'Not Found')) } as unknown as PlayerApiClient;
  render(<ReceiptScreen api={api} sessionId="sX" onBack={() => {}} />);
  expect(await screen.findByText('Чек не найден')).toBeInTheDocument();
});
