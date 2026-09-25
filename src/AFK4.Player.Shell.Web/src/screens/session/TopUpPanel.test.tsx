import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { TopUpPanel } from './TopUpPanel';

const realFetch = globalThis.fetch;
let calls: { path: string; method: string; body: unknown }[] = [];
let bankAnswers: string[] = [];

function serve(options: { online?: boolean; intentStatus?: number; intentQr?: string | null } = {}) {
  globalThis.fetch = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof URL ? input.href : String(input));
    const method = init?.method ?? 'GET';
    calls.push({ path: url.pathname, method, body: init?.body ? JSON.parse(String(init.body)) : null });
    const reply = (status: number, body: unknown) =>
      new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
    if (url.pathname === '/api/me/dashboard') {
      return reply(200, { walletBalance: { currencyCode: 'TJS', minorUnits: 4_500 }, heldBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null });
    }
    if (url.pathname === '/api/me/wallet/top-up-methods') return reply(200, { counter: true, online: options.online ?? true });
    if (url.pathname === '/api/me/wallet/top-up-intent') {
      return reply(options.intentStatus ?? 200, {
        paymentIntentId: 'intent-1', amountMinorUnits: 5_000, currencyCode: 'TJS', state: 'pending', purpose: 'top_up',
        method: 'eskhata', createdAtUtc: '2026-09-25T10:00:00Z', fulfilledAtUtc: null, isExpired: false,
        qr: options.intentQr === undefined ? 'https://pay.example.test/q' : options.intentQr
      });
    }
    if (url.pathname.endsWith('/eskhata-status')) return reply(200, { payment: bankAnswers.shift() ?? 'pending' });
    return reply(404, {});
  }) as unknown as typeof fetch;
}

function renderPanel(onPaid = mock(() => {})) {
  render(
    <I18nProvider initialLocale="ru">
      <TopUpPanel baseUrl="https://api.example.test/" onPaid={onPaid} pollMs={10} />
    </I18nProvider>
  );
  return onPaid;
}

beforeEach(() => {
  calls = [];
  bankAnswers = [];
});

afterEach(() => {
  globalThis.fetch = realFetch;
});

describe('пополнение по QR', () => {
  it('клуб не принимает оплату онлайн — сказано, куда идти, и можно позвать администратора', async () => {
    serve({ online: false });
    renderPanel();

    expect(await screen.findByText(/не принимает оплату онлайн/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Позвать администратора' })).toBeInTheDocument();
  });

  it('сумма → QR банка → «оплачено» только после ответа сервера', async () => {
    serve();
    bankAnswers = ['pending', 'paid'];
    const onPaid = renderPanel();

    const preset = await screen.findByRole('button', { name: /^50/ });
    fireEvent.click(preset);
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Получить QR на 50/ })));

    expect(await screen.findByRole('img', { name: /QR для оплаты 50/ })).toBeInTheDocument();
    expect(calls.find((call) => call.path === '/api/me/wallet/top-up-intent')!.body).toMatchObject({
      amountMinorUnits: 5_000,
      method: 'eskhata'
    });

    expect(await screen.findByText(/Счёт пополнен на 50/)).toBeInTheDocument();
    expect(onPaid).toHaveBeenCalledTimes(1);
  });

  it('банк не провёл — сказано, деньги не списаны, можно выбрать снова', async () => {
    serve();
    bankAnswers = ['failed'];
    renderPanel();

    fireEvent.click(await screen.findByRole('button', { name: /^20 / }));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Получить QR/ })));

    expect(await screen.findByText('Банк не провёл оплату. Деньги не списаны.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Получить QR на 20/ })).toBeEnabled();
  });

  it('банк не дал, что сканировать, — «банк занят», а не пустой квадрат', async () => {
    serve({ intentQr: null });
    renderPanel();

    fireEvent.click(await screen.findByRole('button', { name: /^100/ }));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Получить QR/ })));

    expect(await screen.findByText(/Банк сейчас занят/)).toBeInTheDocument();
    expect(screen.queryByRole('img')).toBeNull();
  });

  it('своя сумма: кривую не отправить, нормальная уходит', async () => {
    serve();
    renderPanel();

    const input = await screen.findByLabelText('Своя сумма');
    fireEvent.change(input, { target: { value: 'abc' } });
    expect(screen.getByText('Введите сумму больше нуля.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Сколько положить' })).toBeDisabled();

    fireEvent.change(input, { target: { value: '35' } });
    expect(screen.getByRole('button', { name: /Получить QR на 35/ })).toBeEnabled();
  });
});
