import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashTopUpRequests } from './CashTopUpRequests';
import type { OperatorTopUpIntentDto } from '../operatorApiClients';

afterEach(cleanup);

function intent(overrides: Partial<OperatorTopUpIntentDto> = {}): OperatorTopUpIntentDto {
  return {
    paymentIntentId: 'intent-1',
    playerAccountId: 'player-1',
    displayName: 'Амир К.',
    amountMinorUnits: 5000,
    currencyCode: 'TJS',
    state: 'pending',
    method: 'counter',
    createdAtUtc: '2026-09-16T08:30:00Z',
    seatName: 'PC-01',
    ...overrides
  };
}

function renderQueue(client: { listPending: () => Promise<OperatorTopUpIntentDto[]>; confirm: () => Promise<unknown> }) {
  return render(
    <I18nProvider initialLocale="ru">
      <CashTopUpRequests backend={null} branchId="branch-1" currencyCode="TJS" client={client} />
    </I18nProvider>
  );
}

describe('CashTopUpRequests', () => {
  it('показывает заявку с именем, местом и суммой', async () => {
    renderQueue({
      listPending: async () => [intent()],
      confirm: async () => ({})
    });

    expect(await screen.findByText('Амир К.')).toBeInTheDocument();
    expect(screen.getByText(/PC-01/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Принять оплату' })).toBeInTheDocument();
  });

  // Деньги по онлайн-оплате приходят от банка. Кнопка рядом с такой заявкой зачислила бы
  // кошелёк за счёт клуба — поэтому её нет, а не «есть, но с предупреждением».
  it('у онлайн-оплаты кнопки приёма нет — её закрывает банк', async () => {
    renderQueue({
      listPending: async () => [intent({ method: 'eskhata' })],
      confirm: async () => ({})
    });

    expect(await screen.findByText('Ждём банк')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Принять оплату' })).toBeNull();
  });

  it('приём оплаты зовёт сервер и перечитывает очередь', async () => {
    const confirm = mock(async () => ({}));
    let call = 0;
    renderQueue({
      listPending: async () => {
        call += 1;
        return call === 1 ? [intent()] : [];
      },
      confirm
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Принять оплату' }));

    await waitFor(() => expect(confirm).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('Заявок на пополнение нет')).toBeInTheDocument();
  });

  // Перечитывание после «Принять оплату» идёт тихо: очередь, которую кассир только что читал,
  // остаётся на месте, пока не придёт новый ответ, — а не пропадает под заглушкой.
  it('после приёма оплаты очередь не пропадает, пока идёт перечитывание', async () => {
    let call = 0;
    renderQueue({
      listPending: () => {
        call += 1;
        return call === 1 ? Promise.resolve([intent(), intent({ paymentIntentId: 'intent-2', displayName: 'Дилшод Р.' })]) : new Promise(() => {});
      },
      confirm: async () => ({})
    });

    fireEvent.click((await screen.findAllByRole('button', { name: 'Принять оплату' }))[0]);
    await waitFor(() => expect(call).toBe(2));
    expect(screen.getByText('Дилшод Р.')).toBeInTheDocument();
  });

  it('пустая очередь объясняет, что здесь появится', async () => {
    renderQueue({ listPending: async () => [], confirm: async () => ({}) });

    expect(await screen.findByText('Заявок на пополнение нет')).toBeInTheDocument();
    expect(screen.getByText(/подают из приложения/)).toBeInTheDocument();
    // Пустая очередь — это хорошо: делать нечего, и кнопки, которая что-то обещает, нет.
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('отказ загрузки показывает причину и кнопку повтора, а не пустой список', async () => {
    let call = 0;
    renderQueue({
      listPending: async () => {
        call += 1;
        if (call === 1) throw new Error('network down');
        return [intent()];
      },
      confirm: async () => ({})
    });

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await screen.findByText('Амир К.')).toBeInTheDocument();
  });
});
