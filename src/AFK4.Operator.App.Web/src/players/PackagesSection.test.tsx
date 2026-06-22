import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PackagesSection } from './PackagesSection';
import type { PlayerPackageDto, PackageOptionDto } from '../operatorApiClients';

afterEach(cleanup);

const pkg = (over: Partial<PlayerPackageDto>): PlayerPackageDto => ({
  playerPackageId: 'pp-1', packageDefinitionId: 'pd-1', playerAccountId: 'p',
  name: 'Ночной 5ч', purchasedPrice: { currencyCode: 'TJS', minorUnits: 25000 },
  includedSeconds: 18000, bonusSeconds: 1800,
  remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800,
  purchasedAtUtc: '2026-06-21T09:00:00Z', expiresAtUtc: null, ...over
});

const option = (over: Partial<PackageOptionDto>): PackageOptionDto => ({
  packageDefinitionId: 'pd-2', name: 'Утренний 2ч', currencyCode: 'TJS',
  priceMinorUnits: 12000, includedSeconds: 7200, bonusSeconds: 1800, expiresAfterDays: 14
} as PackageOptionDto);

const renderSection = (over: Partial<Parameters<typeof PackagesSection>[0]> = {}) => {
  const onSelectOption = mock(() => {});
  const onBuy = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <PackagesSection
        packages={[pkg({})]}
        options={[option({})]}
        selectedPackageDefinitionId="pd-2"
        balanceMinorUnits={50000}
        currencyCode="TJS"
        canPurchase
        onSelectOption={onSelectOption}
        onBuy={onBuy}
        {...over}
      />
    </I18nProvider>
  );
  return { onSelectOption, onBuy };
};

describe('PackagesSection', () => {
  it('renders human-readable package remaining minutes and bonus', () => {
    renderSection();
    expect(screen.getByText('Ночной 5ч')).toBeInTheDocument();
    expect(screen.getByText(/150 мин в пакете/)).toBeInTheDocument();
    expect(screen.getByText(/\+30 бонусных мин/)).toBeInTheDocument();
    expect(screen.getByText('бессрочно')).toBeInTheDocument();
  });

  it('renders the EmptyState when there are no packages', () => {
    renderSection({ packages: [] });
    expect(screen.getByText('Нет активных пакетов')).toBeInTheDocument();
  });

  it('calls onBuy when the purchase button is clicked and affordable', () => {
    const { onBuy } = renderSection();
    fireEvent.click(screen.getByRole('button', { name: /Купить пакет/ }));
    expect(onBuy).toHaveBeenCalled();
  });

  it('disables purchase when balance is below the option price', () => {
    renderSection({ balanceMinorUnits: 0 });
    expect(screen.getByRole('button', { name: /Купить пакет/ })).toBeDisabled();
  });
});
