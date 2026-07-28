import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PackagesSection } from './PackagesSection';
import type { PlayerPackageDto } from '../operatorApiClients';

afterEach(cleanup);

const pkg: PlayerPackageDto = {
  playerPackageId: 'pp-1', packageDefinitionId: 'pd-1', playerAccountId: 'p-1', name: 'Ночной 5ч',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 2000 }, includedSeconds: 18000, bonusSeconds: 1800,
  remainingIncludedSeconds: 5400, remainingBonusSeconds: 1800, purchasedAtUtc: '2026-07-01T00:00:00Z',
  expiresAtUtc: '2099-08-01T00:00:00Z'
};

describe('PackagesSection', () => {
  it('renders package name, included and bonus minutes, expiry and active state', () => {
    render(<I18nProvider initialLocale="ru"><PackagesSection packages={[pkg]} loading={false} /></I18nProvider>);
    expect(screen.getByText('Ночной 5ч')).toBeInTheDocument();
    expect(screen.getByText(/90 мин/)).toBeInTheDocument();
    expect(screen.getByText(/30 бонус/)).toBeInTheDocument();
    expect(screen.getByText('Активен')).toBeInTheDocument();
  });

  it('shows a concrete load error instead of an empty state', () => {
    render(<I18nProvider initialLocale="ru"><PackagesSection packages={[]} loading={false} errorDetail="network down" /></I18nProvider>);
    expect(screen.getByText('network down')).toBeInTheDocument();
    expect(screen.queryByText('Нет активных пакетов')).toBeNull();
  });
});
