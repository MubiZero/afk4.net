import { cleanup, fireEvent, render, screen } from '@testing-library/react';
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
    // Subtitle describes the whole section (catalog + prices + barcodes), deliberately distinct
    // from the «Каталог товаров» POS-section title inside the body — they used to be a verbatim
    // duplicate (eyebrow vs. inner heading saying the same thing).
    expect(container.querySelector('.management-screen-head')?.textContent).toContain('Каталог, цены и штрихкоды');
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

  it('shows a loading skeleton instead of the catalog while loadStatus is loading', () => {
    const { container } = wrap(
      <GoodsDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        catalog={catalog}
        loadStatus="loading"
      />
    );
    expect(container.querySelector('.management-skeleton')).toBeTruthy();
    expect(screen.queryByText('Cola 0.5')).toBeNull();
  });

  it('shows the concrete error detail and retries via onRetry when loadStatus is failed', () => {
    const onRetry = mock(() => {});
    wrap(
      <GoodsDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        catalog={catalog}
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
