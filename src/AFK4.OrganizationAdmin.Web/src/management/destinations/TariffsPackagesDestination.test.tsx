import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
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

  it('shows the tabs and a six-column tariffs table as its loading shape', async () => {
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
    await waitFor(() => expect(container.querySelector('[data-skeleton="table"]')).toBeTruthy());
    expect(container.querySelector('.mgmt-tabs')).toBeTruthy();
    expect(container.querySelectorAll('[data-skeleton="table"] .ctable-head > span')).toHaveLength(6);
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
        failure={{ title: '', detail: 'boom', retryCanHelp: true }}
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('boom')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('keeps tariffs usable while package loading fails and retries only packages', () => {
    const onRetryPackages = mock(() => {});
    wrap(<TariffsPackagesDestination
      backend={null} session={session([])} currencyCode="TJS" tariffs={tariffs} packageOptions={[]}
      packageState={{ status: 'failed', data: [], failure: { title: '', detail: 'Пакеты недоступны', retryCanHelp: true } }} onRetryPackages={onRetryPackages}
    />);
    expect(screen.getByText('Стандарт')).toBeTruthy();
    fireEvent.click(screen.getByRole('tab', { name: 'Пакеты' }));
    expect(screen.getByText('Пакеты недоступны')).toBeTruthy();
    expect(screen.queryByText('Нет пакетов')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetryPackages).toHaveBeenCalledTimes(1);
  });
});

// Раздел открыт по любому из двух прав; вкладка без своего права говорит, что она только для
// просмотра, вместо серых без объяснения кнопок.
describe('TariffsPackagesDestination view-only', () => {
  const backend = { config: { platformBaseUrl: 'http://test' }, branchId: 'b1', session: { accessToken: 't', organizationId: 'o1' } } as never;

  it('marks the tariffs tab view-only for someone who may only manage packages', () => {
    wrap(
      <TariffsPackagesDestination backend={backend} session={session(['organization.packages.manage'])} currencyCode="TJS" tariffs={tariffs} packageOptions={packageOptions} />
    );
    expect(screen.getByRole('note')).toHaveTextContent('Только просмотр: тарифы меняют управляющий и владелец организации.');
    fireEvent.click(screen.getByRole('tab', { name: 'Пакеты' }));
    expect(screen.queryByRole('note')).toBeNull();
  });
});
