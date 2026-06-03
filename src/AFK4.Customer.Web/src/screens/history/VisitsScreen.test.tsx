import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { VisitsScreen } from './VisitsScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function apiWith(page: unknown) {
  return { getVisits: mock().mockResolvedValue(page) } as unknown as PlayerApiClient;
}

it('renders a visit row with seat, total and a receipt link', async () => {
  const api = apiWith({
    items: [{
      sessionId: 's1', seatId: 'seat1', seatName: 'PC-14',
      startedAtUtc: '2026-06-01T10:00:00Z', endedAtUtc: '2026-06-01T12:00:00Z',
      timeChargeMinorUnits: 12000, posTotalMinorUnits: 3000, grandTotalMinorUnits: 15000,
      currencyCode: 'TJS', hasReceipt: true
    }],
    nextCursor: null
  });
  render(<VisitsScreen api={api} onOpenReceipt={() => {}} />);
  expect(await screen.findByText('PC-14')).toBeInTheDocument();
  expect(screen.getByText('150,00 TJS')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /чек/i })).toBeInTheDocument();
});

it('renders an empty state when there are no visits', async () => {
  const api = apiWith({ items: [], nextCursor: null });
  render(<VisitsScreen api={api} onOpenReceipt={() => {}} />);
  expect(await screen.findByText('Пока нет визитов')).toBeInTheDocument();
});
