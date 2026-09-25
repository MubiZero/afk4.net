import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { ShiftTipsDto } from '@afk4/contracts';
import type { OperatorAuthSession } from '../authClient';
import { ShiftTipsSection } from './ShiftTipsSection';

afterEach(cleanup);

const tips: ShiftTipsDto = {
  shiftId: 's1',
  recipientStaffUserId: 'u1',
  recipientName: 'Шерзод',
  total: { currencyCode: 'TJS', minorUnits: 1500 },
  paidOut: { currencyCode: 'TJS', minorUnits: 0 },
  tips: [
    { ledgerEntryId: 't1', amount: { currencyCode: 'TJS', minorUnits: 1000 }, seatLabel: 'ПК 07', createdAtUtc: '2026-09-25T18:00:00Z', reversed: false },
    { ledgerEntryId: 't2', amount: { currencyCode: 'TJS', minorUnits: 500 }, seatLabel: 'ПК 03', createdAtUtc: '2026-09-25T17:00:00Z', reversed: false }
  ]
};

function session(permissions: string[]): OperatorAuthSession {
  return { permissions } as unknown as OperatorAuthSession;
}

function renderTips(permissions: string[], overrides: Partial<{ payOut: () => Promise<ShiftTipsDto>; reverse: () => Promise<ShiftTipsDto>; forShift: () => Promise<ShiftTipsDto> }> = {}) {
  const client = {
    forShift: mock(overrides.forShift ?? (() => Promise.resolve(tips))),
    reverse: mock(overrides.reverse ?? (() => Promise.resolve(tips))),
    payOut: mock(overrides.payOut ?? (() => Promise.resolve({ ...tips, paidOut: { currencyCode: 'TJS', minorUnits: 1500 } })))
  };
  const onShiftChanged = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <ShiftTipsSection session={session(permissions)} shiftId="s1" currencyCode="TJS" client={client} onShiftChanged={onShiftChanged} />
    </I18nProvider>
  );
  return { client, onShiftChanged };
}

describe('ShiftTipsSection', () => {
  it('без чаевых блока нет вовсе', async () => {
    const { client } = renderTips([], { forShift: () => Promise.resolve({ ...tips, tips: [], total: { currencyCode: 'TJS', minorUnits: 0 } }) });
    await waitFor(() => expect(client.forShift).toHaveBeenCalled());
    expect(screen.queryByRole('region', { name: 'Чаевые за смену' })).toBeNull();
  });

  it('выдача из кассы — после подтверждения, и смена обновляется', async () => {
    const { client, onShiftChanged } = renderTips(['organization.shifts.cash.manage']);
    fireEvent.click(await screen.findByRole('button', { name: /Выдать из кассы/ }));
    const dialog = screen.getByRole('alertdialog');
    expect(within(dialog).getByText(/Выдать Шерзод/)).toBeTruthy();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Выдать из кассы' }));

    await waitFor(() => expect(client.payOut).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(onShiftChanged).toHaveBeenCalled());
    expect(await screen.findByText(/Выдано из кассы/)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /Выдать из кассы/ })).toBeNull();
  });

  it('вернуть чаевые может только тот, у кого есть право', async () => {
    renderTips(['organization.shifts.cash.manage']);
    await screen.findByText('ПК 07');
    expect(screen.queryByRole('button', { name: 'Вернуть игроку' })).toBeNull();
    cleanup();

    const { client } = renderTips(['organization.tips.manage']);
    fireEvent.click((await screen.findAllByRole('button', { name: 'Вернуть игроку' }))[0]);
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Вернуть игроку' }));
    await waitFor(() => expect(client.reverse).toHaveBeenCalledWith('s1', 't1'));
    // Без права на кассу кнопки выдачи нет.
    expect(screen.queryByRole('button', { name: /Выдать из кассы/ })).toBeNull();
  });
});
