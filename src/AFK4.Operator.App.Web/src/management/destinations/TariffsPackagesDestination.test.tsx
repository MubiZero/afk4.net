import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { TariffOptionDto } from '../../operatorApiClients';
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

describe('TariffsPackagesDestination', () => {
  it('renders the ManagementScreen title and subtitle', () => {
    wrap(
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
  });

  it('renders the tariff price without an amber/warning class — money is neutral text', () => {
    wrap(
      <TariffsPackagesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        tariffs={tariffs}
        packageOptions={[]}
      />
    );

    const row = screen.getByRole('button', { name: /Стандарт/ });
    const priceEl = row.querySelector('b');
    expect(priceEl).toBeTruthy();
    expect(priceEl!.textContent).toContain('90');
    expect(priceEl!.className).not.toContain('amber');
    expect(priceEl!.className).not.toContain('warning');
    expect(priceEl!.className).toBe('');
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
});
