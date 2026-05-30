import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PackageOption, PlayerPackage } from '@/api/types';
import { PackagesPanel } from './PackagesPanel';

const pkg: PlayerPackage = {
  playerPackageId: 'pp1', packageDefinitionId: 'pd1', playerAccountId: 'p1', name: 'Старт',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 50000 },
  includedSeconds: 3600, bonusSeconds: 600, remainingIncludedSeconds: 1800, remainingBonusSeconds: 300,
  purchasedAtUtc: '2026-05-01T00:00:00.000Z', expiresAtUtc: null
};

const option: PackageOption = {
  packageDefinitionId: 'pd1', name: 'Старт', currencyCode: 'TJS', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

function fakeClient() {
  return {
    getPlayerPackages: vi.fn(async () => [pkg]),
    getPackageOptions: vi.fn(async () => [option]),
    purchasePackage: vi.fn(async () => ({ playerPackageId: 'pp9' }))
  };
}

function renderPanel(canPurchase: boolean) {
  render(
    <I18nProvider><ToastProvider>
      <PackagesPanel client={fakeClient() as never} playerAccountId="p1" branchId="b1" organizationId="org" canPurchase={canPurchase} />
    </ToastProvider></I18nProvider>
  );
}

it('lists the client packages', async () => {
  renderPanel(false);
  expect(await screen.findByText('Старт')).toBeInTheDocument();
  expect(screen.getByText('Пакеты')).toBeInTheDocument();
});

it('hides the purchase trigger when not permitted', async () => {
  renderPanel(false);
  await screen.findByText('Старт');
  expect(screen.queryByRole('button', { name: 'Купить пакет' })).not.toBeInTheDocument();
});

it('opens the purchase dialog when permitted', async () => {
  renderPanel(true);
  await screen.findByText('Старт');
  fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
  expect(await screen.findByText('Покупка пакета')).toBeInTheDocument();
});
