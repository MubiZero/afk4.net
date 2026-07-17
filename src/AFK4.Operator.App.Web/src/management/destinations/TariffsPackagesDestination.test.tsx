import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { PackageOptionDto, TariffOptionDto } from '../../operatorApiClients';
import { TariffsPackagesDestination } from './TariffsPackagesDestination';

afterEach(() => cleanup());

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const tariffs: TariffOptionDto[] = [{
  tariffId: 't1',
  tariffVersionId: 'tv1',
  name: 'Стандарт',
  pricePerMinuteMinorUnits: 150,
  minimumBillableMinutes: 15,
  roundingIncrementMinutes: 5,
  isActive: true,
  currencyCode: 'TJS'
} as never];

const packageOptions: PackageOptionDto[] = [{
  packageDefinitionId: 'p1',
  name: 'Ночной пакет',
  priceMinorUnits: 25000,
  includedSeconds: 300 * 60,
  bonusSeconds: 30 * 60,
  expiresAfterDays: 30,
  isActive: true,
  currencyCode: 'TJS'
} as never];

describe('TariffsPackagesDestination', () => {
  it('renders the ManagementScreen title and subtitle at full content width', () => {
    const { container } = wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={[]}
        packageOptions={[]}
      />
    );

    expect(screen.getByRole('heading', { name: 'Тарифы и пакеты' })).toBeTruthy();
    expect(screen.getByText('Тарифы и пакеты времени')).toBeTruthy();
    expect(container.querySelector('.management-content--full')).toBeTruthy();
  });

  it('defaults to the "Тарифы" tab and lists the tariffs, price shown per hour with no amber class', () => {
    const { container } = wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={tariffs}
        packageOptions={packageOptions}
      />
    );

    expect(screen.getByRole('tab', { name: 'Тарифы', selected: true })).toBeTruthy();
    expect(screen.getByText('Стандарт')).toBeTruthy();
    expect(screen.queryByText('Ночной пакет')).toBeNull();
    const priceEl = container.querySelector('.ui-money');
    expect(priceEl).toBeTruthy();
    expect(priceEl!.textContent).toContain('90'); // 150/min * 60 -> 90.00/hour
    expect(priceEl!.className).toBe('ui-money');
  });

  it('switches to the "Пакеты" tab and shows package definitions instead of tariffs', () => {
    wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={tariffs}
        packageOptions={packageOptions}
      />
    );

    fireEvent.click(screen.getByRole('tab', { name: 'Пакеты' }));
    expect(screen.queryByText('Стандарт')).toBeNull();
    expect(screen.getByText('Ночной пакет')).toBeTruthy();
  });

  it('calls onDirtyChange(false) on mount since the section saves per-action', () => {
    const onDirtyChange = mock(() => {});
    wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={[]}
        packageOptions={[]}
        onDirtyChange={onDirtyChange}
      />
    );
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });

  it('hides the "+ Тариф" action without manageTariffs permission even with a backend', () => {
    wrap(
      <TariffsPackagesDestination
        backend={{ config: {}, session: { permissions: [] }, branchId: 'b1' } as never}
        session={session([])}
        currencyCode="TJS"
        tariffs={tariffs}
        packageOptions={[]}
      />
    );
    expect(screen.queryByRole('button', { name: '+ Тариф' })).toBeNull();
  });

  it('shows a loading skeleton instead of tariffs while loadStatus is loading', () => {
    const { container } = wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={tariffs}
        packageOptions={[]}
        loadStatus="loading"
      />
    );
    expect(container.querySelector('.management-skeleton')).toBeTruthy();
    expect(screen.queryByText('Стандарт')).toBeNull();
  });

  it('shows the concrete error detail and retries via onRetry when loadStatus is failed', () => {
    const onRetry = mock(() => {});
    wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={tariffs}
        packageOptions={[]}
        loadStatus="failed"
        errorDetail="boom"
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('boom')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
