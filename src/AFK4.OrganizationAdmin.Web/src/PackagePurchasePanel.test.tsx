import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PackagePurchasePanel } from './PackagePurchasePanel';
import type { PackageOptionDto } from './operatorApiClients';
import type { PlayerClientItem } from './operatorHelpers';

afterEach(cleanup);

const option: PackageOptionDto = {
  packageDefinitionId: '11111111-1111-1111-1111-111111111111', name: 'Ночной 5ч', currencyCode: 'TJS',
  priceMinorUnits: 2000, includedSeconds: 18000, bonusSeconds: 1800, expiresAfterDays: 30
};
const player: PlayerClientItem & { playerAccountId: string } = {
  playerAccountId: '22222222-2222-2222-2222-222222222222', name: 'Амир', isActive: true,
  status: 'active', balanceMinorUnits: 2_000, debtMinorUnits: 0, last: '', tone: 'active', detail: '',
  phoneNumber: '', source: 'backend', createdAtUtc: null, lastActivityAtUtc: null,
  activePackageName: null, activePackageRemainingMinutes: 0, platformPersonId: null, createdFromApp: false
};
const backend = {
  config: { platformBaseUrl: 'http://test' }, branchId: 'branch-1',
  session: { accessToken: 't', organizationId: 'org', permissions: ['organization.packages.purchase'] }
};

describe('PackagePurchasePanel', () => {
  it('purchases the selected package through the player package endpoint', async () => {
    const purchasePackage = mock(async () => ({}));
    const onPurchased = mock(() => {});
    render(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={backend as never} player={player} options={[option]} shiftOpen onPurchased={onPurchased} purchasePackage={purchasePackage as never} /></I18nProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
    await waitFor(() => expect(purchasePackage).toHaveBeenCalledWith(player.playerAccountId, {
      organizationId: 'org', packageDefinitionId: option.packageDefinitionId, idempotencyKey: expect.any(String)
    }));
    expect(onPurchased).toHaveBeenCalled();
  });

  it('says the package was bought', async () => {
    const purchasePackage = mock(async () => ({ name: option.name }));
    render(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={backend as never} player={player} options={[option]} shiftOpen onPurchased={() => {}} purchasePackage={purchasePackage as never} /></I18nProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
    expect(await screen.findByRole('status')).toHaveTextContent('Пакет «Ночной 5ч» куплен.');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('does not report a completed purchase as failed when only the screen refresh fails', async () => {
    // Деньги уже списаны, ключ попытки сброшен: красная строка здесь — приглашение купить второй раз.
    const purchasePackage = mock(async () => ({ name: option.name }));
    const onPurchased = mock(async () => { throw new TypeError('Failed to fetch'); });
    render(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={backend as never} player={player} options={[option]} shiftOpen onPurchased={onPurchased} purchasePackage={purchasePackage as never} /></I18nProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
    const status = await screen.findByRole('status');
    expect(status).toHaveTextContent('Пакет «Ночной 5ч» куплен');
    expect(status).toHaveTextContent('покупать ещё раз не нужно');
    expect(screen.queryByRole('alert')).toBeNull();
    expect(purchasePackage).toHaveBeenCalledTimes(1);
  });

  it('retries a purchase whose answer was lost with the same idempotency key', async () => {
    const keys: string[] = [];
    let calls = 0;
    const purchasePackage = mock(async (_player: string, body: { idempotencyKey: string }) => {
      keys.push(body.idempotencyKey);
      calls += 1;
      if (calls === 1) throw new TypeError('Failed to fetch');
      return { name: option.name };
    });
    render(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={backend as never} player={player} options={[option]} shiftOpen onPurchased={() => {}} purchasePackage={purchasePackage as never} /></I18nProvider>);
    fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
    await screen.findByRole('alert');
    fireEvent.click(screen.getByRole('button', { name: 'Купить пакет' }));
    await screen.findByRole('status');
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it('does not allow purchase without permission, an active player, or an open shift', () => {
    const purchasePackage = mock(async () => ({}));
    const denied = { ...backend, session: { ...backend.session, permissions: [] } } as never;
    const { rerender } = render(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={denied} player={player} options={[option]} shiftOpen onPurchased={() => {}} purchasePackage={purchasePackage as never} /></I18nProvider>);
    expect(screen.getByRole('button', { name: 'Купить пакет' })).toBeDisabled();
    rerender(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={backend as never} player={{ ...player, isActive: false }} options={[option]} shiftOpen onPurchased={() => {}} purchasePackage={purchasePackage as never} /></I18nProvider>);
    expect(screen.getByRole('button', { name: 'Купить пакет' })).toBeDisabled();
    rerender(<I18nProvider initialLocale="ru"><PackagePurchasePanel backend={backend as never} player={player} options={[option]} shiftOpen={false} onPurchased={() => {}} purchasePackage={purchasePackage as never} /></I18nProvider>);
    expect(screen.getByRole('button', { name: 'Купить пакет' })).toBeDisabled();
    expect(purchasePackage).not.toHaveBeenCalled();
  });
});
