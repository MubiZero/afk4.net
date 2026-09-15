import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { TopUpScreen } from './TopUpScreen';
import type { ShellApi } from '../shellApi';

function fakeApi(over: Partial<ShellApi>): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [],
    createTopUpIntent: async () => ({ paymentIntentId: 'p1', amountMinorUnits: 5000, currencyCode: 'TJS',
      state: 'pending', purpose: 'wallet_topup', method: 'dcgate', createdAtUtc: '', fulfilledAtUtc: null,
      isExpired: false, payUrl: 'pay.dc.tj/abc', comment: '123456789012345678', gatewayExpiresAtUtc: null }),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    ...over
  } as ShellApi;
}

describe('TopUpScreen', () => {
  it('shows the QR comment after creating an intent', async () => {
    render(<TopUpScreen api={fakeApi({})} amountMinorUnits={5000} pollIntervalMs={5} />);
    await waitFor(() => expect(screen.getByText(/123456789012345678/)).toBeInTheDocument());
    await waitFor(() => expect(screen.getByTestId('topup-qr')).toBeInTheDocument());
  });

  // Банк присылает собственный QR, и только он. Раньше экран требовал `payUrl` и без него не
  // показывал кода вовсе — то есть ровно в том случае, ради которого банк код и выдаёт.
  it('рисует код банка, даже когда веб-ссылки нет', async () => {
    const api = fakeApi({
      createTopUpIntent: async () => ({
        paymentIntentId: 'p1', amountMinorUnits: 5000, currencyCode: 'TJS', state: 'pending',
        purpose: 'wallet_topup', method: 'eskhata', createdAtUtc: '', fulfilledAtUtc: null,
        isExpired: false, payUrl: null, comment: 'счёт 42', gatewayExpiresAtUtc: null,
        qr: '00020101021226580014', deepLink: null
      })
    });
    render(<TopUpScreen api={api} amountMinorUnits={5000} pollIntervalMs={5} />);
    await waitFor(() => expect(screen.getByTestId('topup-qr')).toBeInTheDocument());
  });

  it('shows success once the intent is fulfilled', async () => {
    let polls = 0;
    const api = fakeApi({
      getTopUpIntents: async () => { polls++;
        return [{ paymentIntentId: 'p1', amountMinorUnits: 5000, currencyCode: 'TJS',
          state: polls >= 2 ? 'fulfilled' : 'pending', purpose: 'wallet_topup', method: 'dcgate',
          createdAtUtc: '', fulfilledAtUtc: null, isExpired: false, payUrl: 'pay.dc.tj/abc',
          comment: '123456789012345678', gatewayExpiresAtUtc: null }]; }
    });
    render(<TopUpScreen api={api} amountMinorUnits={5000} pollIntervalMs={5} />);
    await waitFor(() => expect(screen.getByText(/успешно|success/i)).toBeInTheDocument(), { timeout: 2000 });
  });
});
