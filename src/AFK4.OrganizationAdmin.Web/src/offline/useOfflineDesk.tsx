import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import type { TariffOptionDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { SeatSummary } from '../operatorData';
import { createAuthenticatedOperatorClients, tariffPriceForMinutes } from '../operatorHelpers';
import { useToast } from '../operatorToast';
import { offlineDeskClient, type OfflineDeskClient } from './offlineDeskClient';
import { readOfflineDeskCache, writeOfflineDeskCache, type OfflineDeskCache } from './offlineCache';
import {
  extensionPrice,
  foldOfflineSessions,
  liveOfflineSessionsBySeat,
  tariffAppliesAt,
  type OfflineJournalRecord,
  type OfflineSession
} from './offlineSessions';

/** Сколько живёт аренда «конец сессии»: ПК запирается сразу, срок лишь ограничивает её силу. */
const END_LEASE_MS = 5 * 60_000;
/** Как часто, пока связь есть, обновлять тарифы и смену в кэше. */
const CACHE_REFRESH_MS = 5 * 60_000;

export interface OfflineDesk {
  /** Хост есть, право сажать гостей есть, кэш тарифов и смены тоже: офлайн-касса работает. */
  ready: boolean;
  /** Почему офлайн-касса не работает — для одной строки в карточке места. */
  unavailableReason: 'no-host' | 'no-cache' | 'no-shift' | null;
  isOffline: boolean;
  sessionsBySeat: Map<string, OfflineSession>;
  pendingCount: number;
  tariffsNow: TariffOptionDto[];
  seatGuest: (seat: SeatSummary, tariff: TariffOptionDto, minutes: number) => Promise<void>;
  extend: (session: OfflineSession, minutes: number) => Promise<void>;
  end: (session: OfflineSession) => Promise<void>;
  priceFor: (tariff: TariffOptionDto, minutes: number) => number;
  extensionPriceFor: (session: OfflineSession, minutes: number) => number;
}

const OfflineDeskContext = createContext<OfflineDesk | null>(null);

export function OfflineDeskProvider({ value, children }: { value: OfflineDesk; children: ReactNode }) {
  return <OfflineDeskContext.Provider value={value}>{children}</OfflineDeskContext.Provider>;
}

export function useOfflineDeskContext(): OfflineDesk | null {
  return useContext(OfflineDeskContext);
}

/**
 * Офлайн у стойки (docs/roadmap/offline-front-desk.md). Без связи — посадить гостя за наличные, продлить,
 * закончить: ПК открывает подписанная ключом этого ПК аренда, деньги ложатся в журнал на диске. Пока
 * связь есть — кэш тарифов и смены, ключ на сервере; когда связь вернулась — журнал уходит на сервер.
 */
export function useOfflineDesk({
  backend,
  isOffline,
  canSeat,
  client = offlineDeskClient
}: {
  backend: OperatorBackendContext | null;
  isOffline: boolean;
  canSeat: boolean;
  client?: OfflineDeskClient;
}): OfflineDesk {
  const { t } = useI18n();
  const toast = useToast();
  const hostAvailable = client.available() && canSeat && backend !== null;
  const branchId = backend?.branchId ?? null;
  const [records, setRecords] = useState<OfflineJournalRecord[]>([]);
  const [cache, setCache] = useState<OfflineDeskCache | null>(() => (branchId ? readOfflineDeskCache(branchId) : null));
  const [, setTick] = useState(0);
  const syncing = useRef(false);
  const registeredFor = useRef<string | null>(null);

  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    [backend]
  );

  const reload = useCallback(async () => {
    if (!hostAvailable) return;
    try {
      setRecords(await client.read());
    } catch {
      // Журнал не прочитать — офлайн-сессий на карте не покажем, но и денег не потеряем: они на диске.
    }
  }, [client, hostAvailable]);

  useEffect(() => { void reload(); }, [reload]);
  useEffect(() => { setCache(branchId ? readOfflineDeskCache(branchId) : null); }, [branchId]);

  // Карточка места считает «осталось» сама, но место освобождается по времени — раз в полминуты
  // перечитываем, какие офлайн-сессии ещё идут.
  useEffect(() => {
    const timer = window.setInterval(() => setTick((value) => value + 1), 30_000);
    return () => window.clearInterval(timer);
  }, []);

  // Пока связь есть: тарифы и открытая смена — в кэш, ключ этого ПК — на сервер.
  useEffect(() => {
    if (!hostAvailable || isOffline || !clients || !branchId) return undefined;
    let disposed = false;
    const refresh = async () => {
      try {
        const [tariffs, shift] = await Promise.all([
          clients.settings.getTariffOptions(branchId),
          clients.shifts.getCurrentShift(branchId)
        ]);
        if (disposed) return;
        const next = { tariffs, shiftId: shift?.shiftId ?? null, cachedAtMs: Date.now() };
        writeOfflineDeskCache(branchId, next);
        setCache(next);
      } catch {
        // Не удалось обновить — остаётся прежний кэш.
      }

      if (registeredFor.current !== branchId) {
        try {
          const key = await client.key();
          await clients.offlineDesk.registerKey(branchId, { keyId: key.keyId, publicKeyPem: key.publicKeyPem });
          registeredFor.current = branchId;
        } catch {
          // Повторим со следующим обновлением.
        }
      }
    };
    void refresh();
    const timer = window.setInterval(() => void refresh(), CACHE_REFRESH_MS);
    return () => { disposed = true; window.clearInterval(timer); };
  }, [branchId, client, clients, hostAvailable, isOffline]);

  // Связь вернулась — журнал на сервер. Убираем из журнала только то, на что пришёл окончательный ответ.
  useEffect(() => {
    if (!hostAvailable || isOffline || !clients || !branchId || records.length === 0 || syncing.current) return;
    syncing.current = true;
    void (async () => {
      try {
        const response = await clients.offlineDesk.uploadJournal(branchId, { records: records.map(toServerRecord) });
        await client.acknowledge(response.results.map((result) => result.id));
        const rejected = response.results.filter((result) => result.status === 'rejected').length;
        const applied = response.results.length - rejected;
        if (applied > 0) toast.success(t('op.offline.synced', { count: applied }));
        if (rejected > 0) toast.error(t('op.offline.syncRejected', { count: rejected }));
        await reload();
      } catch {
        // Не дошло — журнал на месте, попробуем со следующим изменением.
      } finally {
        syncing.current = false;
      }
    })();
  }, [branchId, client, clients, hostAvailable, isOffline, records, reload, t, toast]);

  const tariffs = cache?.tariffs ?? [];
  const sessions = useMemo(() => foldOfflineSessions(records, tariffs), [records, tariffs]);
  const sessionsBySeat = liveOfflineSessionsBySeat(sessions, Date.now());
  const tariffsNow = useMemo(() => tariffs.filter((tariff) => tariffAppliesAt(tariff, new Date())), [tariffs]);

  const unavailableReason: OfflineDesk['unavailableReason'] = !hostAvailable
    ? 'no-host'
    : !cache || cache.tariffs.length === 0
      ? 'no-cache'
      : !cache.shiftId
        ? 'no-shift'
        : null;

  const send = useCallback(async (address: string, lease: Parameters<OfflineDeskClient['sendLease']>[1]) => {
    const response = await client.sendLease(address, lease);
    if (!response.accepted) throw new Error(response.reason ?? 'refused');
  }, [client]);

  const append = useCallback(async (record: OfflineJournalRecord) => {
    await client.append(record);
    setRecords((current) => [...current, record]);
  }, [client]);

  const seatGuest = useCallback(async (seat: SeatSummary, tariff: TariffOptionDto, minutes: number) => {
    if (!backend || !cache?.shiftId || !seat.deviceId || !seat.deviceNetworkAddress) throw new Error('not-ready');
    const sessionId = crypto.randomUUID();
    const issuedAt = new Date();
    const expiresAt = new Date(issuedAt.getTime() + minutes * 60_000);
    const lease = {
      sessionId, organizationId: backend.session.organizationId, branchId: backend.branchId, seatId: seat.id,
      deviceId: seat.deviceId, state: 'Active', sequence: 1, issuedAtUtc: issuedAt.toISOString(), expiresAtUtc: expiresAt.toISOString()
    };
    await send(seat.deviceNetworkAddress, lease);
    try {
      await append({
        id: crypto.randomUUID(), kind: 'session_started', sessionId, occurredAtUtc: issuedAt.toISOString(), shiftId: cache.shiftId,
        seatId: seat.id, deviceId: seat.deviceId, deviceAddress: seat.deviceNetworkAddress, tariffRuleVersionId: tariff.tariffRuleVersionId,
        tariffName: tariff.name, minutes, chargeMinorUnits: tariffPriceForMinutes(tariff, minutes), currencyCode: tariff.currencyCode,
        sequence: 1, leaseExpiresAtUtc: lease.expiresAtUtc
      });
    } catch (error) {
      // ПК открыт, а запись о деньгах не легла — запираем обратно: открытый ПК без записи — бесплатная игра.
      await send(seat.deviceNetworkAddress, { ...lease, state: 'Ended', sequence: 2, expiresAtUtc: new Date(Date.now() + END_LEASE_MS).toISOString() });
      throw error;
    }
  }, [append, backend, cache, send]);

  const extend = useCallback(async (session: OfflineSession, minutes: number) => {
    if (!backend || !cache?.shiftId) throw new Error('not-ready');
    const now = new Date();
    const base = Math.max(Date.parse(session.endsAtUtc), now.getTime());
    const expiresAt = new Date(base + minutes * 60_000).toISOString();
    const sequence = session.sequence + 1;
    await send(session.deviceAddress, {
      sessionId: session.sessionId, organizationId: backend.session.organizationId, branchId: backend.branchId, seatId: session.seatId,
      deviceId: session.deviceId, state: 'Active', sequence, issuedAtUtc: now.toISOString(), expiresAtUtc: expiresAt
    });
    await append({
      id: crypto.randomUUID(), kind: 'session_extended', sessionId: session.sessionId, occurredAtUtc: now.toISOString(), shiftId: cache.shiftId,
      minutes, chargeMinorUnits: extensionPrice(session, minutes), currencyCode: session.currencyCode, sequence, leaseExpiresAtUtc: expiresAt
    });
  }, [append, backend, cache, send]);

  const end = useCallback(async (session: OfflineSession) => {
    if (!backend || !cache?.shiftId) throw new Error('not-ready');
    const now = new Date();
    const sequence = session.sequence + 1;
    await send(session.deviceAddress, {
      sessionId: session.sessionId, organizationId: backend.session.organizationId, branchId: backend.branchId, seatId: session.seatId,
      deviceId: session.deviceId, state: 'Ended', sequence, issuedAtUtc: now.toISOString(),
      expiresAtUtc: new Date(now.getTime() + END_LEASE_MS).toISOString()
    });
    await append({
      id: crypto.randomUUID(), kind: 'session_ended', sessionId: session.sessionId, occurredAtUtc: now.toISOString(), shiftId: cache.shiftId,
      sequence, leaseExpiresAtUtc: now.toISOString()
    });
  }, [append, backend, cache, send]);

  return {
    ready: unavailableReason === null,
    unavailableReason,
    isOffline,
    sessionsBySeat,
    pendingCount: records.length,
    tariffsNow,
    seatGuest,
    extend,
    end,
    priceFor: tariffPriceForMinutes,
    extensionPriceFor: extensionPrice
  };
}

/** На сервер — только его поля: адрес ПК и аренда нужны лишь Панели, пока нет связи. */
function toServerRecord(record: OfflineJournalRecord) {
  return {
    id: record.id, kind: record.kind, sessionId: record.sessionId, occurredAtUtc: record.occurredAtUtc, shiftId: record.shiftId,
    seatId: record.seatId ?? null, deviceId: record.deviceId ?? null, tariffRuleVersionId: record.tariffRuleVersionId ?? null,
    minutes: record.minutes ?? 0, chargeMinorUnits: record.chargeMinorUnits ?? 0, currencyCode: record.currencyCode ?? null
  };
}

/**
 * Карта без связи: места, где по журналу сидит гость, — заняты, со своим «осталось». Сервер о них ещё не
 * знает, поэтому без наложения карта показала бы их свободными, и кассир посадил бы второго гостя.
 */
export function withOfflineSessions(seats: SeatSummary[], sessionsBySeat: Map<string, OfflineSession>, guestLabel: string): SeatSummary[] {
  if (sessionsBySeat.size === 0) return seats;
  return seats.map((seat) => {
    const session = sessionsBySeat.get(seat.id);
    if (!session) return seat;
    const deadline = Date.parse(session.endsAtUtc);
    return {
      ...seat,
      tone: 'active',
      player: guestLabel,
      hasActiveSession: true,
      activeSessionId: session.sessionId,
      sessionState: 'Active',
      sessionBillingMode: 'prepaid_cash',
      remainingSeconds: Math.max(0, Math.round((deadline - Date.now()) / 1000)),
      remainingDeadlineMs: deadline,
      sessionStartedAtUtc: session.startedAtUtc,
      tariffName: session.tariffName,
      isOfflineSession: true
    };
  });
}
