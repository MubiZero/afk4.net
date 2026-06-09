import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { SelfServiceMenu } from './SelfServiceMenu';
import type { ShellApi } from '../shellApi';

function api(): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    listShopCatalog: async () => [{ productId: 'p1', name: 'Cola', sku: 'C', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 5 }],
    listShopOrders: async () => [], placeShopOrder: async () => ({} as any), cancelShopOrder: async () => ({} as any)
  } as unknown as ShellApi;
}

describe('SelfServiceMenu', () => {
  it('shows login when not authenticated', () => {
    render(<SelfServiceMenu authenticated={false} onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b" onReloadState={() => {}} />);
    expect(screen.getByLabelText(/телефон|phone/i)).toBeInTheDocument();
  });

  it('shows the menu when authenticated and opens extend', async () => {
    render(<SelfServiceMenu authenticated={true} onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b" onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /продлить/i }));
    await waitFor(() => expect(screen.getByText(/продлить время/i)).toBeInTheDocument());
  });
});

describe('SelfServiceMenu shop entry', () => {
  it('opens the shop from the menu', async () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /магазин/i }));
    await waitFor(() => expect(screen.getByText('Cola')).toBeInTheDocument());
  });
});
