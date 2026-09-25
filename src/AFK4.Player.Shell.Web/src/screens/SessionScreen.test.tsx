import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { PlayerSelfEndSessionResponse, ShellAuthStateDto } from '@afk4/contracts';
import { devExtendOffers, devScenarioState } from '../host/devHost';
import { installFakeHost } from '../test/fakeHost';
import { SessionScreen } from './SessionScreen';

const owner: ShellAuthStateDto = { signedIn: true, displayName: 'Алишер', playerAccountId: '00000000-0000-4000-8000-000000000020' };
const realFetch = globalThis.fetch;
let calls: { url: string; method: string; body: unknown }[] = [];

function serve(handler: (path: string, method: string) => { status: number; body: unknown }) {
  globalThis.fetch = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof URL ? input.href : String(input));
    const method = init?.method ?? 'GET';
    calls.push({ url: url.pathname, method, body: init?.body ? JSON.parse(String(init.body)) : null });
    const { status, body } = handler(url.pathname, method);
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

const endResult: PlayerSelfEndSessionResponse = {
  billedMinutes: 35,
  refunded: { currencyCode: 'TJS', minorUnits: 1_000 },
  packageMinutesReturned: 0
};

function api(path: string, method: string) {
  if (path.endsWith('/extend-offers')) return { status: 200, body: devExtendOffers(Date.now()) };
  if (path.endsWith('/end-quote')) return { status: 200, body: { billedMinutes: 35, refund: { currencyCode: 'TJS', minorUnits: 1_000 }, packageMinutesReturned: 0 } };
  if (path.endsWith('/end') && method === 'POST') return { status: 200, body: endResult };
  return { status: 200, body: {} };
}

function renderSession(options: {
  auth?: ShellAuthStateDto;
  variant?: 'session' | 'ending' | 'grace';
  kind?: string;
  onEnded?: (result: PlayerSelfEndSessionResponse) => void;
  onSignIn?: () => void;
} = {}) {
  installFakeHost({ state: null });
  const state = { ...devScenarioState(options.variant ?? 'session')!, ...(options.kind ? { sessionOwnerKind: options.kind, sessionOwnerPlayerAccountId: null } : {}) };
  return render(
    <I18nProvider initialLocale="ru">
      <SessionScreen
        state={state}
        receivedAtMs={Date.now()}
        variant={options.variant ?? 'session'}
        auth={options.auth}
        onEnded={options.onEnded}
        onSignIn={options.onSignIn}
      />
    </I18nProvider>
  );
}

beforeEach(() => {
  calls = [];
  serve(api);
});

afterEach(() => {
  globalThis.fetch = realFetch;
});

describe('экран сессии', () => {
  it('владелец видит «Продлить» и «Встать раньше» рядом с остатком', () => {
    renderSession({ auth: owner });

    expect(screen.getByRole('button', { name: 'Продлить' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Встать раньше' })).toBeEnabled();
    expect(screen.getByTestId('countdown')).toBeInTheDocument();
  });

  it('гостю стойки денежных кнопок нет — продлевает администратор', () => {
    renderSession({ kind: 'guest' });

    expect(screen.queryByRole('button', { name: 'Продлить' })).toBeNull();
    expect(screen.getByText('Продлить эту сессию можно у администратора.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Позвать администратора' })).toBeInTheDocument();
  });

  it('владелец, севший с телефона, может войти, чтобы продлить', () => {
    const onSignIn = mock(() => {});
    renderSession({ onSignIn });

    fireEvent.click(screen.getByRole('button', { name: 'Войти' }));

    expect(onSignIn).toHaveBeenCalled();
  });

  it('без связи с клубом продлить и встать раньше нельзя — и сказано почему', () => {
    renderSession({ auth: owner, variant: 'grace' });

    expect(screen.getByRole('button', { name: 'Продлить' })).toBeDisabled();
    expect(screen.getByText('Без связи с клубом продлить нельзя.')).toBeInTheDocument();
  });

  it('продление: варианты с сервера, уходит число минут и ключ, экран говорит «продлено до»', async () => {
    renderSession({ auth: owner });

    fireEvent.click(screen.getByRole('button', { name: 'Продлить' }));
    const hour = await screen.findByRole('button', { name: /^\+1 ч,/ });
    await act(async () => fireEvent.click(hour));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Продлить · 10/ })));

    const posted = calls.find((call) => call.method === 'POST')!;
    expect(posted.url).toBe('/api/me/sessions/00000000-0000-4000-8000-000000000010/extend');
    expect(posted.body).toMatchObject({ additionalMinutes: 60 });
    expect(typeof (posted.body as { idempotencyKey: unknown }).idempotencyKey).toBe('string');
    expect(await screen.findByText(/Продлено до/)).toBeInTheDocument();
  });

  it('ранний выход сначала говорит, сколько вернётся, и только потом заканчивает', async () => {
    const onEnded = mock((_result: PlayerSelfEndSessionResponse) => {});
    renderSession({ auth: owner, onEnded });

    fireEvent.click(screen.getByRole('button', { name: 'Встать раньше' }));
    expect(await screen.findByText(/Вернём на счёт 10/)).toBeInTheDocument();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Встать и освободить ПК' })));

    await waitFor(() => expect(onEnded).toHaveBeenCalledWith(endResult));
  });

  it('«Играть дальше» закрывает лист и ничего не заканчивает', async () => {
    renderSession({ auth: owner });

    fireEvent.click(screen.getByRole('button', { name: 'Встать раньше' }));
    await screen.findByText(/Вернём на счёт/);
    fireEvent.click(screen.getByRole('button', { name: 'Играть дальше' }));

    expect(screen.queryByRole('dialog')).toBeNull();
    expect(calls.some((call) => call.method === 'POST')).toBe(false);
  });

  it('у владельца есть вкладка бара, если у клуба он включён', async () => {
    serve((path, method) => path.includes('/api/me/shop/') ? { status: 200, body: [] } : api(path, method));
    renderSession({ auth: owner });

    fireEvent.click(screen.getByRole('tab', { name: 'Бар' }));

    expect(screen.getByRole('tab', { name: 'Бар' })).toHaveAttribute('aria-selected', 'true');
    expect(await screen.findByText(/Клуб не выложил меню/)).toBeInTheDocument();
  });

  it('без входа и без бара у клуба вкладок нет — заказывать не с чего', () => {
    renderSession({});
    expect(screen.queryByRole('tab', { name: 'Бар' })).toBeNull();
  });

  it('сессию по пакету продлевают иначе — лист так и говорит', async () => {
    serve((path, method) => path.endsWith('/extend-offers')
      ? { status: 200, body: { ...devExtendOffers(Date.now()), options: [], unavailableReason: 'package_session' } }
      : api(path, method));
    renderSession({ auth: owner });

    fireEvent.click(screen.getByRole('button', { name: 'Продлить' }));

    expect(await screen.findByText(/продлевают новым стартом/)).toBeInTheDocument();
  });
});
