import { describe, expect, it, afterEach } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PackagesSection } from './PackagesSection';
import type { PlayerPackageDto } from '../operatorApiClients';

afterEach(cleanup);

const pkg = (over: Partial<PlayerPackageDto>): PlayerPackageDto => ({
  playerPackageId: 'pp-1', packageDefinitionId: 'pd-1', playerAccountId: 'p',
  name: 'Ночной 5ч', purchasedPrice: { currencyCode: 'TJS', minorUnits: 25000 },
  includedSeconds: 18000, bonusSeconds: 1800,
  remainingIncludedSeconds: 9000, remainingBonusSeconds: 1800,
  purchasedAtUtc: '2026-06-21T09:00:00Z', expiresAtUtc: null, ...over
});

const renderSection = (over: Partial<Parameters<typeof PackagesSection>[0]> = {}) => render(
  <I18nProvider initialLocale="ru">
    <PackagesSection
      packages={[pkg({})]}
      loading={false}
      {...over}
    />
  </I18nProvider>
);

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

  it('shows a skeleton instead of the package list while loading', () => {
    const { container } = renderSection({ loading: true });
    expect(container.querySelector('.client-package-skeleton')).not.toBeNull();
    expect(screen.queryByText('Ночной 5ч')).toBeNull();
  });
});
