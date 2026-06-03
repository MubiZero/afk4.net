import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PurchasesScreen } from './PurchasesScreen';
import type { PlayerApiClient } from '@/api/playerApi';

it('renders a purchase with its lines and total', async () => {
  const api = { getPurchases: mock().mockResolvedValue({
    items: [{
      posSaleId: 'q1', createdAtUtc: '2026-06-02T15:00:00Z', totalMinorUnits: 4500, currencyCode: 'TJS',
      lines: [{ productName: 'Энергетик', quantity: 3, unitPriceMinorUnits: 1500, lineTotalMinorUnits: 4500 }]
    }],
    nextCursor: null
  }) } as unknown as PlayerApiClient;
  render(<I18nProvider><PurchasesScreen api={api} /></I18nProvider>);
  expect(await screen.findByText('Энергетик × 3')).toBeInTheDocument();
  expect(screen.getByText('45,00 TJS')).toBeInTheDocument();
});

it('renders an empty state when there are no purchases', async () => {
  const api = { getPurchases: mock().mockResolvedValue({ items: [], nextCursor: null }) } as unknown as PlayerApiClient;
  render(<I18nProvider><PurchasesScreen api={api} /></I18nProvider>);
  expect(await screen.findByText('Пока нет покупок')).toBeInTheDocument();
});
