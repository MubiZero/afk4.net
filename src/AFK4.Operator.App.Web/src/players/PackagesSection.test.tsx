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
  priceMinorUnits: 12000, includedSeconds: 7200, bonusSeconds: 1800, expiresAfterDays: 14,
  ...over
} as PackageOptionDto);

const renderSection = (over: Partial<Parameters<typeof PackagesSection>[0]> = {}) => {
  const onSelectOption = mock(() => {});
  const onBuy = mock(() => {});
  const view = render(
    <I18nProvider initialLocale="ru">
      <PackagesSection
        packages={[pkg({})]}
        options={[option({})]}
        selectedPackageDefinitionId="pd-2"
        balanceMinorUnits={50000}
        currencyCode="TJS"
        canPurchase
        busy={false}
        loading={false}
        onSelectOption={onSelectOption}
        onBuy={onBuy}
        {...over}
      />
    </I18nProvider>
  );
  return { onSelectOption, onBuy, ...view };
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

  it('splits the preview minutes into included, bonus and total', () => {
    renderSection();
    // included 120, bonus +30, total 150 — разнесены, не двусмысленный «итого»
    expect(screen.getByText('Включено')).toBeInTheDocument();
    expect(screen.getByText('120 мин')).toBeInTheDocument();
    expect(screen.getByText('+30 мин')).toBeInTheDocument();
    expect(screen.getByText('Итого')).toBeInTheDocument();
    expect(screen.getByText('150 мин')).toBeInTheDocument();
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

  it('shows how much the deposit is short instead of a generic message', () => {
    renderSection({ balanceMinorUnits: 0 });
    expect(screen.getByText(/не хватает/)).toBeInTheDocument();
  });

  it('disables the button and shows a pending label while a purchase is in flight', () => {
    renderSection({ busy: true });
    const button = screen.getByRole('button', { name: /Покупаем/ });
    expect(button).toBeDisabled();
    // купить-кнопка не должна оставаться кликабельной → защита от двойного списания
    expect(screen.queryByRole('button', { name: /^Купить пакет$/ })).toBeNull();
  });

  it('shows a sale-empty state instead of a zero preview when there are no options', () => {
    renderSection({ options: [] });
    expect(screen.getByText('Пакетов в продаже нет')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Купить пакет/ })).toBeNull();
  });

  it('shows a skeleton instead of the package list while loading', () => {
    const { container } = renderSection({ loading: true });
    expect(container.querySelector('.client-package-skeleton')).not.toBeNull();
    expect(screen.queryByText('Ночной 5ч')).toBeNull();
  });
});
