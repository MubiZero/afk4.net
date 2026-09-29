import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { OwedShiftTipsDto, ShiftTipsDto } from '@afk4/contracts';
import type { OperatorAuthSession } from '../authClient';
import { cashReasonLabel } from '../operatorHelpers';
import { OwedTipsSection } from './OwedTipsSection';
import { PlatformApiError } from '../platformApi';

afterEach(cleanup);

const owedRow: OwedShiftTipsDto = {
  shiftId: 'old-shift',
  recipientStaffUserId: 'u1',
  recipientName: 'Шерзод',
  openedAtUtc: '2026-09-27T08:00:00Z',
  closedAtUtc: '2026-09-27T20:00:00Z',
  owed: { currencyCode: 'TJS', minorUnits: 1500 }
};

function renderOwed(permissions: string[], owed: OwedShiftTipsDto[] = [owedRow]) {
  let current = owed;
  const client = {
    owed: mock(async () => current),
    payOut: mock(async (_shiftId: string) => {
      current = [];
      return {} as ShiftTipsDto;
    })
  };
  const onShiftChanged = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <OwedTipsSection
        client={client}
        session={{ permissions } as unknown as OperatorAuthSession}
        branchId="b1"
        currencyCode="TJS"
        onShiftChanged={onShiftChanged}
      />
    </I18nProvider>
  );
  return { client, onShiftChanged };
}

describe('OwedTipsSection', () => {
  // Отказ списка раньше глотался: блок пропадал, и это читалось как «долгов по чаевым нет», хотя
  // деньги персонала из закрытых смен ждут выдачи. Теперь — причина и «Повторить».
  it('не загрузился — говорит об этом и перезапрашивает по «Повторить»', async () => {
    let calls = 0;
    const client = {
      owed: mock(async () => {
        calls += 1;
        if (calls === 1) throw new PlatformApiError('boom', 503, 'Service Unavailable', '');
        return [owedRow];
      }),
      payOut: mock(async () => ({} as ShiftTipsDto))
    };
    render(
      <I18nProvider initialLocale="ru">
        <OwedTipsSection client={client} session={{ permissions: [] } as unknown as OperatorAuthSession} branchId="b1" currencyCode="TJS" onShiftChanged={() => {}} />
      </I18nProvider>
    );

    expect(await screen.findByRole('alert')).toHaveTextContent('Долг по чаевым не загрузился');
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('region', { name: 'Чаевые прошлых смен к выдаче' })).toBeInTheDocument();
    expect(client.owed).toHaveBeenCalledTimes(2);
  });

  it('долгов нет — блока нет', async () => {
    const { client } = renderOwed(['organization.shifts.cash.manage'], []);
    await waitFor(() => expect(client.owed).toHaveBeenCalled());
    expect(screen.queryByRole('region', { name: 'Чаевые прошлых смен к выдаче' })).toBeNull();
  });

  // Смену закрыли, а чаевые не выдали: долг виден и выдаётся из кассы открытой смены.
  it('невыданное прошлой смены выдаётся после подтверждения, и касса обновляется', async () => {
    const { client, onShiftChanged } = renderOwed(['organization.shifts.cash.manage']);

    fireEvent.click(await screen.findByRole('button', { name: 'Выдать из кассы' }));
    expect(client.payOut).not.toHaveBeenCalled();
    fireEvent.click(screen.getAllByRole('button', { name: 'Выдать из кассы' }).at(-1)!);

    await waitFor(() => expect(client.payOut).toHaveBeenCalledTimes(1));
    expect(client.payOut.mock.calls[0][0]).toBe('old-shift');
    expect(onShiftChanged).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByRole('region', { name: 'Чаевые прошлых смен к выдаче' })).toBeNull());
  });

  it('без права на кассу долг виден, а выдать нельзя', async () => {
    renderOwed([]);
    expect(await screen.findByText(/Шерзод/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Выдать из кассы' })).toBeNull();
  });
});

describe('cashReasonLabel', () => {
  const t = ((key: string, values?: Record<string, string>) => `${key}|${values?.name ?? ''}`) as never;

  it('причину, которую пишет сервер кодом, подписывает на языке экрана', () => {
    expect(cashReasonLabel('tip_payout:Шерзод', t)).toBe('op.cash.reason.tipPayout|Шерзод');
  });

  it('причину, введённую сотрудником, показывает как есть', () => {
    expect(cashReasonLabel('Размен', t)).toBe('Размен');
    expect(cashReasonLabel(null, t)).toBe('');
  });
});
