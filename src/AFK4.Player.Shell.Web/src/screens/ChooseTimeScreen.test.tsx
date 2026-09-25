import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { ShellI18nProvider } from '../i18n/ShellI18nProvider';
import { devScenarioState, devStartOffers } from '../host/devHost';
import { installFakeHost } from '../test/fakeHost';
import { ChooseTimeScreen } from './ChooseTimeScreen';

const realFetch = globalThis.fetch;
let calls: { url: string; method: string; body: unknown }[] = [];

function serve(handler: (url: URL, method: string) => { status: number; body: unknown }) {
  globalThis.fetch = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof URL ? input.href : String(input));
    const method = init?.method ?? 'GET';
    calls.push({ url: url.pathname, method, body: init?.body ? JSON.parse(String(init.body)) : null });
    const { status, body } = handler(url, method);
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

function renderScreen() {
  installFakeHost({ state: null });
  const state = { ...devScenarioState('idle')!, apiBaseUrl: 'https://api.example.test/' };
  return render(
    <ShellI18nProvider initialLocale="ru">
      <ChooseTimeScreen state={state} auth={{ signedIn: true, displayName: 'Алишер', playerAccountId: null }} />
    </ShellI18nProvider>
  );
}

beforeEach(() => {
  calls = [];
});

afterEach(() => {
  cleanup();
  globalThis.fetch = realFetch;
});

describe('сколько играем', () => {
  it('показывает цены с сервера и баланс — ничего не считает сам', async () => {
    serve(() => ({ status: 200, body: devStartOffers(Date.now()) }));
    renderScreen();

    expect(await screen.findByText('Стандарт')).toBeInTheDocument();
    expect(screen.getByText(/На счёте 45/)).toBeInTheDocument();
    expect(calls[0]).toEqual({ url: '/api/me/this-pc/start-offers', method: 'GET', body: null });
  });

  it('на что не хватает денег — выбрать нельзя, и видно сколько не хватает', async () => {
    serve(() => ({ status: 200, body: devStartOffers(Date.now()) }));
    renderScreen();

    const fiveHours = within(await screen.findByRole('group', { name: 'Стандарт' })).getByRole('button', { name: /^5 ч,/ });
    expect(fiveHours).toBeDisabled();
    expect(fiveHours).toHaveTextContent('Не хватает 5');
  });

  it('тариф, который ещё не начался, говорит когда', async () => {
    serve(() => ({ status: 200, body: devStartOffers(Date.now()) }));
    renderScreen();

    expect(await screen.findByText(/Действует с/)).toBeInTheDocument();
  });

  it('старт уходит без кода посадки и ждёт ответа, не празднуя заранее', async () => {
    serve((url) => url.pathname.endsWith('/start') ? { status: 200, body: {} } : { status: 200, body: devStartOffers(Date.now()) });
    renderScreen();

    const standard = await screen.findByRole('group', { name: 'Стандарт' });
    await act(async () => fireEvent.click(within(standard).getByRole('button', { name: /^2 ч,/ })));
    const start = screen.getByRole('button', { name: /Начать · 20/ });
    await act(async () => fireEvent.click(start));

    const posted = calls.find((call) => call.method === 'POST')!;
    expect(posted.url).toBe('/api/me/sessions/start');
    expect(posted.body).toMatchObject({
      seatingCode: '',
      tariffRuleVersionId: '00000000-0000-4000-8000-000000000101',
      durationMinutes: 120,
      playerPackageId: null
    });
    expect(screen.getByRole('button', { name: 'Запускаем…' })).toBeDisabled();
  });

  it('по пакету уходят минуты и пакет, без тарифа', async () => {
    serve((url) => url.pathname.endsWith('/start') ? { status: 200, body: {} } : { status: 200, body: devStartOffers(Date.now()) });
    renderScreen();

    // Ждать — снаружи act: внутри него React не применяет обновления, и ожидание запирает само себя.
    const allOfIt = await screen.findByRole('button', { name: /^3 ч 20 мин,/ });
    await act(async () => fireEvent.click(allOfIt));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Начать по пакету' })));

    expect(calls.find((call) => call.method === 'POST')!.body).toMatchObject({
      tariffRuleVersionId: '',
      durationMinutes: 200,
      playerPackageId: '00000000-0000-4000-8000-000000000201'
    });
  });

  it('не хватило денег к моменту старта — говорит и перечитывает цены', async () => {
    serve((url) => url.pathname.endsWith('/start')
      ? { status: 409, body: { error: 'insufficient_balance' } }
      : { status: 200, body: devStartOffers(Date.now()) });
    renderScreen();

    const standard = await screen.findByRole('group', { name: 'Стандарт' });
    await act(async () => fireEvent.click(within(standard).getByRole('button', { name: /^1 ч,/ })));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Начать/ })));

    expect(await screen.findByText(/Не хватает денег на счёте/)).toBeInTheDocument();
    await waitFor(() => expect(calls.filter((call) => call.method === 'GET')).toHaveLength(2));
  });

  it('пополнить можно прямо отсюда, если у клуба есть онлайн-оплата', async () => {
    serve(() => ({ status: 200, body: devStartOffers(Date.now()) }));
    renderScreen();

    expect(await screen.findByRole('button', { name: 'Пополнить' })).toBeInTheDocument();
  });

  it('цены не загрузились — можно попробовать ещё раз', async () => {
    serve(() => ({ status: 500, body: {} }));
    renderScreen();

    expect(await screen.findByText('Цены не загрузились.')).toBeInTheDocument();
    serve(() => ({ status: 200, body: devStartOffers(Date.now()) }));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Ещё раз' })));

    expect(await screen.findByText('Стандарт')).toBeInTheDocument();
  });
});
