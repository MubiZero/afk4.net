import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { PosProductDto } from '../../operatorApiClients';
import { GoodsDestination } from './GoodsDestination';

afterEach(() => cleanup());

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const catalog: PosProductDto[] = [{
  productId: 'p1',
  name: 'Cola 0.5',
  sku: 'COLA-05',
  price: { currencyCode: 'TJS', minorUnits: 1000 },
  trackStock: true,
  stockOnHand: 5
} as never];

describe('GoodsDestination', () => {
  it('renders the ManagementScreen title and subtitle', () => {
    const { container } = wrap(
      <GoodsDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        catalog={[]}
      />
    );

    expect(screen.getByRole('heading', { name: 'Товары' })).toBeTruthy();
    // «Каталог товаров» is coincidentally also the POS-section title inside the body, so scope
    // the subtitle assertion to the screen head to avoid a duplicate-text match.
    expect(container.querySelector('.management-screen-head')?.textContent).toContain('Каталог товаров');
  });

  it('renders the catalog passed in from ManagementWorkspace state', () => {
    wrap(
      <GoodsDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        catalog={catalog}
      />
    );

    expect(screen.getByText('Cola 0.5')).toBeTruthy();
  });

  it('does not render the stock-movement control — that lives in Склад now', () => {
    wrap(
      <GoodsDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        catalog={catalog}
      />
    );

    expect(screen.queryByRole('button', { name: 'Записать движение' })).toBeNull();
  });

  it('calls onDirtyChange(false) on mount since the section saves per-action', () => {
    const onDirtyChange = mock(() => {});
    wrap(
      <GoodsDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        catalog={[]}
        onDirtyChange={onDirtyChange}
      />
    );
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });
});
