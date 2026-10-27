import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import type { ReactNode } from 'react';
import type { TariffOptionDto } from '@afk4/contracts';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { SeatSummary } from '../operatorData';
import { ToastProvider } from '../operatorToast';
import type { OfflineDeskClient } from './offlineDeskClient';
import { writeOfflineDeskCache } from './offlineCache';
import type { OfflineJournalRecord } from './offlineSessions';
import { OfflineDeskProvider, useOfflineDesk, withOfflineSessions, type OfflineDesk } from './useOfflineDesk';
import { OfflineSeatActions } from './OfflineSeatActions';
import { OfflineCashScreen } from './OfflineCashScreen';

const tariff = {
  tariffRuleVersionId: 'tariff-v1', name: 'Дневной', currencyCode: 'TJS', pricePerMinuteMinorUnits: 200,
  minimumBillableMinutes: 60, roundingIncrementMinutes: 15, appliesOnDaysMask: 0, appliesFromMinuteOfDay: null, appliesToMinuteOfDay: null
} as unknown as TariffOptionDto;

const seat = { id: 'seat-1', name: 'PC-01', deviceId: 'pc-1', deviceNetworkAddress: '192.168.1.21', activeSessionId: null } as unknown as SeatSummary;

const backend = {
  branchId: 'branch-a',
  config: { platformBaseUrl: 'https://platform.test/', currencyCode: 'TJS', organizationId: 'org-1', branchId: 'branch-a' },
  session: { staffUserId: 'staff-1', organizationId: 'org-1', accessToken: 'token', permissions: ['organization.sessions.start'] }
} as unknown as OperatorBackendContext;

function fakeClient(overrides: Partial<OfflineDeskClient> = {}) {
  const journal: OfflineJournalRecord[] = [];
  const client = {
    available: () => true,
    key: mock(async () => ({ keyId: 'desk-1', publicKeyPem: 'pem' })),
    sendLease: mock(async () => ({ accepted: true, reason: null, isLocked: false, activeSessionId: null, leaseExpiresAtUtc: null })),
    append: mock(async (record: OfflineJournalRecord) => { journal.push(record); return { appended: true }; }),
    read: mock(async () => [...journal]),
    acknowledge: mock(async () => ({ remaining: 0 })),
    ...overrides
  } as unknown as OfflineDeskClient;
  return { client, journal };
}

const wrapper = ({ children }: { children: ReactNode }) => <I18nProvider><ToastProvider>{children}</ToastProvider></I18nProvider>;

afterEach(() => { cleanup(); window.localStorage.clear(); });

describe('касса без связи', () => {
  it('сначала открывает ПК, потом пишет деньги в журнал — и место на карте занято', async () => {
    writeOfflineDeskCache('branch-a', { tariffs: [tariff], shiftId: 'shift-1', cachedAtMs: Date.now() });
    const { client, journal } = fakeClient();
    const { result } = renderHook(() => useOfflineDesk({ backend, isOffline: true, canSeat: true, client }), { wrapper });
    await waitFor(() => expect(result.current.ready).toBe(true));

    await act(() => result.current.seatGuest(seat, tariff, 60));

    expect(client.sendLease).toHaveBeenCalledTimes(1);
    expect(journal[0]).toMatchObject({ kind: 'session_started', seatId: 'seat-1', chargeMinorUnits: 12000, shiftId: 'shift-1', sequence: 1 });
    await waitFor(() => expect(result.current.sessionsBySeat.get('seat-1')).toBeDefined());
    const [overlaid] = withOfflineSessions([seat], result.current.sessionsBySeat, 'Гость');
    expect(overlaid.tone).toBe('active');
    expect(overlaid.isOfflineSession).toBe(true);
  });

  // ПК открыт, а запись о деньгах не легла: открытый ПК без записи — бесплатная игра. Запираем обратно.
  it('если запись не легла, запирает ПК обратно', async () => {
    writeOfflineDeskCache('branch-a', { tariffs: [tariff], shiftId: 'shift-1', cachedAtMs: Date.now() });
    const { client } = fakeClient({ append: mock(async () => { throw new Error('disk'); }) });
    const { result } = renderHook(() => useOfflineDesk({ backend, isOffline: true, canSeat: true, client }), { wrapper });
    await waitFor(() => expect(result.current.ready).toBe(true));

    await expect(result.current.seatGuest(seat, tariff, 60)).rejects.toThrow('disk');

    const leases = (client.sendLease as ReturnType<typeof mock>).mock.calls.map((call) => (call[1] as { state: string }).state);
    expect(leases).toEqual(['Active', 'Ended']);
  });

  it('без открытой смены деньги не берёт', async () => {
    writeOfflineDeskCache('branch-a', { tariffs: [tariff], shiftId: null, cachedAtMs: Date.now() });
    const { client } = fakeClient();
    const { result } = renderHook(() => useOfflineDesk({ backend, isOffline: true, canSeat: true, client }), { wrapper });

    await waitFor(() => expect(result.current.unavailableReason).toBe('no-shift'));
  });

  it('карточка места: цена названа, кнопка сажает', async () => {
    const seatGuest = mock(async () => undefined);
    const desk = {
      ready: true, unavailableReason: null, isOffline: true, sessionsBySeat: new Map(), pendingCount: 0, tariffsNow: [tariff],
      seatGuest, extend: mock(async () => undefined), end: mock(async () => undefined),
      priceFor: () => 12000, extensionPriceFor: () => 3000
    } as unknown as OfflineDesk;
    render(<I18nProvider><OfflineDeskProvider value={desk}><OfflineSeatActions seat={seat} /></OfflineDeskProvider></I18nProvider>);

    fireEvent.click(screen.getByRole('button', { name: /Взял наличными/ }));

    await waitFor(() => expect(seatGuest).toHaveBeenCalledWith(seat, tariff, 60));
  });

  it('продажа и пересменка: деньги после закрытия смены ложатся в новую', async () => {
    writeOfflineDeskCache('branch-a', {
      tariffs: [tariff], shiftId: 'shift-1', shiftOpenedByStaffUserId: 'staff-1', currencyCode: 'TJS', cachedAtMs: Date.now(),
      products: [{ productId: 'cola', name: 'Cola', priceMinorUnits: 1200, currencyCode: 'TJS' }]
    });
    const { client, journal } = fakeClient();
    const cash = { sell: true, openShift: true, closeShift: false, closeOwnShift: true };
    const { result } = renderHook(() => useOfflineDesk({ backend, isOffline: true, canSeat: true, cash, client }), { wrapper });
    await waitFor(() => expect(result.current.canCloseShift).toBe(true));

    await act(() => result.current.sell([{ productId: 'cola', name: 'Cola', quantity: 2, unitPriceMinorUnits: 1200 }]));
    await act(() => result.current.closeShift(2400, 'handover'));
    await waitFor(() => expect(result.current.shiftId).toBeNull());
    await act(() => result.current.openShift(1000, ''));
    await waitFor(() => expect(result.current.shiftId).not.toBeNull());
    await act(() => result.current.sell([{ productId: 'cola', name: 'Cola', quantity: 1, unitPriceMinorUnits: 1200 }]));

    expect(journal.map((row) => row.kind)).toEqual(['sale', 'shift_closed', 'shift_opened', 'sale']);
    expect(journal[0]).toMatchObject({ shiftId: 'shift-1', chargeMinorUnits: 2400, currencyCode: 'TJS' });
    expect(journal[1]).toMatchObject({ shiftId: 'shift-1', cashMinorUnits: 2400, note: 'handover' });
    expect(journal[3].shiftId).toBe(journal[2].shiftId);
  });

  // Оператор сдаёт только свою смену — та же граница, что проверит сервер у журнала.
  it('чужую смену оператор без связи не закрывает', async () => {
    writeOfflineDeskCache('branch-a', { tariffs: [tariff], shiftId: 'shift-1', shiftOpenedByStaffUserId: 'someone-else', cachedAtMs: Date.now() });
    const { client } = fakeClient();
    const cash = { sell: true, openShift: true, closeShift: false, closeOwnShift: true };
    const { result } = renderHook(() => useOfflineDesk({ backend, isOffline: true, canSeat: true, cash, client }), { wrapper });
    await waitFor(() => expect(result.current.ready).toBe(true));

    expect(result.current.canCloseShift).toBe(false);
  });

  it('экран кассы: товар в чек, «взял наличными» продаёт по цене из кэша', async () => {
    const sell = mock(async () => undefined);
    const desk = {
      isOffline: true, unavailableReason: null, pendingCount: 0, cachedAtMs: Date.now(), shiftId: 'shift-1', currencyCode: 'TJS',
      products: [{ productId: 'cola', name: 'Cola', priceMinorUnits: 1200, currencyCode: 'TJS' }],
      canSell: true, canCloseShift: true, canOpenShift: true, sell, closeShift: mock(async () => undefined), openShift: mock(async () => undefined)
    } as unknown as OfflineDesk;
    render(<I18nProvider><ToastProvider><OfflineDeskProvider value={desk}><OfflineCashScreen /></OfflineDeskProvider></ToastProvider></I18nProvider>);

    fireEvent.click(screen.getByRole('button', { name: /Cola/ }));
    fireEvent.click(screen.getByRole('button', { name: /Больше: Cola/ }));
    fireEvent.click(screen.getByRole('button', { name: /Взял наличными/ }));

    await waitFor(() => expect(sell).toHaveBeenCalledWith([{ productId: 'cola', name: 'Cola', quantity: 2, unitPriceMinorUnits: 1200 }]));
  });

  // Закрытие — в два нажатия: смену не сдают случайным кликом.
  it('экран кассы: смена закрывается только после подтверждения', async () => {
    const closeShift = mock(async () => undefined);
    const desk = {
      isOffline: true, unavailableReason: null, pendingCount: 0, cachedAtMs: Date.now(), shiftId: 'shift-1', currencyCode: 'TJS', products: [],
      canSell: true, canCloseShift: true, canOpenShift: true, sell: mock(async () => undefined), closeShift, openShift: mock(async () => undefined)
    } as unknown as OfflineDesk;
    render(<I18nProvider><ToastProvider><OfflineDeskProvider value={desk}><OfflineCashScreen /></OfflineDeskProvider></ToastProvider></I18nProvider>);

    fireEvent.change(screen.getByLabelText('Наличные в ящике'), { target: { value: '150,50' } });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    expect(closeShift).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: /Точно закрыть/ }));

    await waitFor(() => expect(closeShift).toHaveBeenCalledWith(15050, ''));
  });

  it('со связью карточка ничего не добавляет', () => {
    const desk = { isOffline: false } as unknown as OfflineDesk;
    const { container } = render(<I18nProvider><OfflineDeskProvider value={desk}><OfflineSeatActions seat={seat} /></OfflineDeskProvider></I18nProvider>);
    expect(container.innerHTML).toBe('');
  });
});
