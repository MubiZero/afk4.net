import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BranchesScreen } from './BranchesScreen';
import type { OperatorDashboardSummary } from '@/api/types';

function summary(id: string, online: number): OperatorDashboardSummary {
  return {
    organizationId: 'org', branchId: id, fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 10, activeSessions: 2, endingSessions: 0, onlineDevices: online, offlineDevices: 1, sessionStarts: 0, utilizationPercent: 20 },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 1, endingSessions: 0, totalAlerts: 3 },
    revenue: { posNetSales: { amount: 0, currencyCode: 'RUB' }, gameplayRevenue: { amount: 0, currencyCode: 'RUB' }, totalRevenue: { amount: 1000, currencyCode: 'RUB' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

function fakeClient() {
  return {
    getBranchProfile: vi.fn(async (id: string) => ({ organizationId: 'org', branchId: id, name: id === 'a' ? 'Центр' : 'Юг', city: 'Москва', createdAtUtc: '' })),
    getDashboardSummary: vi.fn(async (id: string) => summary(id, id === 'a' ? 5 : 3)),
    updateBranchProfile: vi.fn()
  };
}

function setup(client = fakeClient(), onOpenBranch = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <BranchesScreen client={client as never} branchIds={['a', 'b']} organizationId="org" onOpenBranch={onOpenBranch} />
    </ToastProvider></I18nProvider>
  );
  return { client, onOpenBranch };
}

it('renders a card per branch with its real name', async () => {
  setup();
  expect(await screen.findByText('Центр')).toBeInTheDocument();
  expect(screen.getByText('Юг')).toBeInTheDocument();
});

it('opens a branch via its Открыть button', async () => {
  const { onOpenBranch } = setup();
  const openButtons = await screen.findAllByRole('button', { name: 'Открыть' });
  fireEvent.click(openButtons[0]);
  expect(onOpenBranch).toHaveBeenCalledWith('a');
});

it('renames a branch through the rename dialog', async () => {
  const client = fakeClient();
  client.updateBranchProfile = vi.fn().mockResolvedValue({});
  setup(client);
  const renameButtons = await screen.findAllByRole('button', { name: 'Переименовать' });
  fireEvent.click(renameButtons[0]);
  fireEvent.change(await screen.findByLabelText('Название филиала'), { target: { value: 'Новый центр' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalledWith('a', { organizationId: 'org', name: 'Новый центр', city: 'Москва' }));
});

it('shows the add-branch affordance as unavailable', async () => {
  setup();
  expect(await screen.findByText('Создание филиалов выполняется при подключении — обратитесь в поддержку.')).toBeInTheDocument();
});
