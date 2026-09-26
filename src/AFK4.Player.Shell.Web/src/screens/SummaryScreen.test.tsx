import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { ShellI18nProvider } from '../i18n/ShellI18nProvider';
import type { PlayerSelfEndSessionResponse } from '@afk4/contracts';
import { devScenarioState } from '../host/devHost';
import { SummaryScreen } from './SummaryScreen';

const realFetch = globalThis.fetch;
let posted: unknown[] = [];

const tipOffer = {
  available: true, unavailableReason: null, recipientName: 'Шерзод', given: null,
  balance: { currencyCode: 'TJS', minorUnits: 1_500 },
  presets: [500, 1_000, 2_000].map((minorUnits) => ({ currencyCode: 'TJS', minorUnits }))
};

function serve(options: { receipt?: boolean; reviewStatus?: number; reviewError?: string; tip?: Record<string, unknown> | null } = {}) {
  globalThis.fetch = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof URL ? input.href : String(input));
    const reply = (status: number, body: unknown) =>
      new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
    if (url.pathname.endsWith('/receipt')) {
      return options.receipt === false
        ? reply(404, {})
        : reply(200, {
            receiptNumber: 'S-1', createdAtUtc: '2026-09-25T11:35:00Z', sessionId: 's-1', seatName: 'ПК 07',
            startedAtUtc: '2026-09-25T10:00:00Z', endedAtUtc: '2026-09-25T11:35:00Z',
            timeChargeMinorUnits: 1_600, posLines: [], posTotalMinorUnits: 2_400, grandTotalMinorUnits: 4_000, currencyCode: 'TJS'
          });
    }
    if (url.pathname.endsWith('/tip')) {
      if (init?.method === 'POST') {
        posted.push(JSON.parse(String(init.body)));
        return reply(200, { amount: { currencyCode: 'TJS', minorUnits: 1_000 }, balanceAfter: { currencyCode: 'TJS', minorUnits: 500 }, recipientName: 'Шерзод' });
      }
      return options.tip === null ? reply(404, {}) : reply(200, options.tip ?? tipOffer);
    }
    if (url.pathname === '/api/me/reviews') {
      posted.push(JSON.parse(String(init?.body)));
      return reply(options.reviewStatus ?? 200, options.reviewError ? { error: options.reviewError } : {});
    }
    return reply(404, {});
  }) as unknown as typeof fetch;
}

function renderSummary(selfEnd: PlayerSelfEndSessionResponse | null = null) {
  const onPlayMore = mock(() => {});
  const onLeave = mock(() => {});
  render(
    <ShellI18nProvider initialLocale="ru">
      <SummaryScreen
        state={devScenarioState('idle')!}
        visit={{ sessionId: 's-1', selfEnd }}
        baseUrl="https://api.example.test/"
        activity={0}
        onPlayMore={onPlayMore}
        onLeave={onLeave}
      />
    </ShellI18nProvider>
  );
  return { onPlayMore, onLeave };
}

beforeEach(() => {
  posted = [];
});

afterEach(() => {
  globalThis.fetch = realFetch;
});

describe('итог визита', () => {
  it('после таймера — сколько сыграно и во что обошлось, по чеку', async () => {
    serve();
    renderSummary();

    expect(await screen.findByText('Сыграно 1 ч 35 мин')).toBeInTheDocument();
    expect(screen.getByText(/За время 16/)).toBeInTheDocument();
    expect(screen.getByText(/Бар 24/)).toBeInTheDocument();
    expect(screen.getByText(/Итого 40/)).toBeInTheDocument();
  });

  it('после раннего выхода — и что вернулось', async () => {
    serve();
    renderSummary({ billedMinutes: 35, refunded: { currencyCode: 'TJS', minorUnits: 1_000 }, packageMinutesReturned: 20 });

    expect(await screen.findByText(/Вернули на счёт 10/)).toBeInTheDocument();
    expect(screen.getByText('В пакет вернулось 20 мин')).toBeInTheDocument();
  });

  it('чека нет — итог говорит то, что знает, без выдуманных сумм', async () => {
    serve({ receipt: false });
    renderSummary({ billedMinutes: 35, refunded: { currencyCode: 'TJS', minorUnits: 0 }, packageMinutesReturned: 0 });

    expect(await screen.findByText('Сыграно 35 мин')).toBeInTheDocument();
    expect(screen.queryByText(/Итого/)).toBeNull();
    expect(screen.queryByText(/Вернули/)).toBeNull();
  });

  it('оценка уходит звёздами и комментарием, потом «спасибо»', async () => {
    serve();
    renderSummary();

    fireEvent.click(screen.getByRole('button', { name: '4 звезды' }));
    fireEvent.change(screen.getByLabelText(/Что понравилось/), { target: { value: '  Быстрые ПК  ' } });
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Отправить оценку' })));

    expect(posted).toEqual([{ sessionId: 's-1', rating: 4, comment: 'Быстрые ПК' }]);
    expect(await screen.findByText('Спасибо! Клуб увидит вашу оценку.')).toBeInTheDocument();
  });

  it('визит уже оценили с телефона — для человека это то же «спасибо»', async () => {
    serve({ reviewStatus: 409, reviewError: 'already_reviewed' });
    renderSummary();

    fireEvent.click(screen.getByRole('button', { name: '5 звёзд' }));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Отправить оценку' })));

    expect(await screen.findByText(/Спасибо/)).toBeInTheDocument();
  });

  it('«Играть ещё» и «Выйти» делают своё', () => {
    serve();
    const { onPlayMore, onLeave } = renderSummary();

    fireEvent.click(screen.getByRole('button', { name: 'Играть ещё' }));
    fireEvent.click(screen.getByRole('button', { name: 'Выйти' }));

    expect(onPlayMore).toHaveBeenCalledTimes(1);
    expect(onLeave).toHaveBeenCalledTimes(1);
  });

  it('чаевые администратору: сумма, подтверждение, «спасибо» — только после ответа', async () => {
    serve();
    renderSummary();

    const ten = await screen.findByRole('button', { name: '10 с.' });
    // 20 с. больше баланса — сумма серая.
    expect((screen.getByRole('button', { name: '20 с.' }) as HTMLButtonElement).disabled).toBe(true);
    fireEvent.click(ten);
    expect(screen.getByText(/^С баланса спишется 10\s?с\.$/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Оставить чаевые' }));

    expect(await screen.findByText(/^Спасибо! 10\s?с\. — Шерзод\.$/)).toBeTruthy();
    const body = posted.at(-1) as { amount: { minorUnits: number }; idempotencyKey: string };
    expect(body.amount.minorUnits).toBe(1_000);
    expect(body.idempotencyKey).toBeTruthy();
  });

  it('клуб чаевые не включил — блока нет', async () => {
    serve({ tip: { ...tipOffer, available: false, unavailableReason: 'disabled' } });
    renderSummary();

    await screen.findByText(/Итого/);
    expect(screen.queryByRole('button', { name: '10 с.' })).toBeNull();
  });
});

