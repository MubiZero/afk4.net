import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { SelfServiceMenu } from './SelfServiceMenu';
import type { ShellApi } from '../shellApi';

function api(): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    listShopCatalog: async () => [{ productId: 'p1', name: 'Cola', sku: 'C', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 5 }],
    listShopOrders: async () => [], placeShopOrder: async () => ({} as any), cancelShopOrder: async () => ({} as any),
    getLoyalty: async () => ({
      topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0,
      totalEarned: { currencyCode: 'TJS', minorUnits: 0 }, recent: []
    })
  } as unknown as ShellApi;
}

describe('SelfServiceMenu', () => {
  it('shows login when not authenticated', () => {
    render(<SelfServiceMenu authenticated={false} onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b" features={null} onReloadState={() => {}} />);
    expect(screen.getByLabelText(/телефон|phone/i)).toBeInTheDocument();
  });

  it('shows the menu when authenticated and opens extend', async () => {
    render(<SelfServiceMenu authenticated={true} onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b" features={null} onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /продлить/i }));
    await waitFor(() => expect(screen.getByText(/продлить время/i)).toBeInTheDocument());
  });
});

describe('SelfServiceMenu shop entry', () => {
  it('opens the shop from the menu', async () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={null} onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /магазин/i }));
    await waitFor(() => expect(screen.getByText('Cola')).toBeInTheDocument());
  });
});

describe('SelfServiceMenu loyalty entry', () => {
  it('opens loyalty from the menu', async () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={null} onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /кэшбэк/i }));
    await waitFor(() => expect(screen.getByText(/падает прямо в кошелёк|кэшбэк пока недоступен/i)).toBeInTheDocument());
  });
});

describe('SelfServiceMenu feature toggles', () => {
  it('прячет пункт «Магазин», когда магазин выключен', () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={['online_booking', 'loyalty', 'online_topup']} onReloadState={() => {}} />);
    expect(screen.queryByRole('button', { name: /магазин/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /кэшбэк/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /пополнить/i })).toBeInTheDocument();
  });

  it('прячет пункт «Бонусы», когда лояльность выключена', () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={['online_booking', 'online_topup', 'player_shop']} onReloadState={() => {}} />);
    expect(screen.queryByRole('button', { name: /кэшбэк/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /магазин/i })).toBeInTheDocument();
  });

  it('прячет пункт «Пополнить», когда онлайн-пополнение выключено', () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={['online_booking', 'loyalty', 'player_shop']} onReloadState={() => {}} />);
    expect(screen.queryByRole('button', { name: /пополнить/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /магазин/i })).toBeInTheDocument();
  });

  it('показывает все пункты, когда включено всё', () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={['online_booking', 'loyalty', 'online_topup', 'player_shop']} onReloadState={() => {}} />);
    expect(screen.getByRole('button', { name: /магазин/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /кэшбэк/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /пополнить/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /продлить/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /новости/i })).toBeInTheDocument();
  });

  it('показывает все пункты, если список фич не пришёл', () => {
    // features === null: список недоступен (не загрузился/сбой) → фичи считаются включёнными,
    // fail-open (см. комментарий в App.tsx) — интерфейс не защита, сервер и так откажет 403.
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" features={null} onReloadState={() => {}} />);
    expect(screen.getByRole('button', { name: /магазин/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /кэшбэк/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /пополнить/i })).toBeInTheDocument();
  });
});
