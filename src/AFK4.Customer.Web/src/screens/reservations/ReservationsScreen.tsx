import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerReservationDto } from '@/api/types';
import { formatDateTime } from '@/lib/datetime';
import { useToast } from '@/components/ui/toast';

const STATE_KEYS: Record<string, MessageKey> = {
  pending: 'customer.reservations.statePending',
  confirmed: 'customer.reservations.stateConfirmed',
  seated: 'customer.reservations.stateSeated',
  cancelled: 'customer.reservations.stateCancelled'
};

export function ReservationsScreen({ api, phoneVerified }: { api: PlayerApiClient; phoneVerified: boolean }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [reservations, setReservations] = useState<PlayerReservationDto[] | null>(null);
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [pending, setPending] = useState(false);

  // Guard against setState after unmount (navigation away mid-fetch).
  const mountedRef = useRef(true);
  useEffect(() => () => { mountedRef.current = false; }, []);

  const refresh = useCallback(() => {
    api.getReservations()
      .then((list) => { if (mountedRef.current) setReservations(list); })
      .catch(() => { if (mountedRef.current) setReservations([]); });
  }, [api]);

  useEffect(() => { refresh(); }, [refresh]);

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    if (!startsAt || !endsAt) {
      toast({ title: t('customer.reservations.timeError'), variant: 'error' });
      return;
    }
    setPending(true);
    try {
      await api.createReservation({
        startsAtUtc: new Date(startsAt).toISOString(),
        endsAtUtc: new Date(endsAt).toISOString()
      });
      setStartsAt('');
      setEndsAt('');
      toast({ title: t('customer.reservations.created'), variant: 'success' });
      refresh();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      toast({ title: status === 409 ? t('customer.reservations.conflict') : t('customer.reservations.createError'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function handleCancel(reservationId: string) {
    if (!globalThis.confirm(t('customer.reservations.cancelConfirm'))) return;
    try {
      await api.cancelReservation(reservationId);
      toast({ title: t('customer.reservations.cancelled'), variant: 'success' });
      refresh();
    } catch {
      toast({ title: t('customer.reservations.cancelError'), variant: 'error' });
    }
  }

  return (
    <main className="space-y-5 px-6 py-6">
      <h1 className="text-2xl font-extrabold tracking-tight">{t('customer.reservations.title')}</h1>

      {phoneVerified ? (
        <form className="space-y-3 rounded-2xl bg-[var(--color-surface)] p-4" onSubmit={handleCreate}>
          <div className="space-y-1.5">
            <label htmlFor="res-start" className="text-sm text-[var(--text-2)]">{t('customer.reservations.start')}</label>
            <input id="res-start" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)}
              className="h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]" />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="res-end" className="text-sm text-[var(--text-2)]">{t('customer.reservations.end')}</label>
            <input id="res-end" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)}
              className="h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]" />
          </div>
          <button type="submit" disabled={pending}
            className="h-11 w-full rounded-xl bg-[var(--accent)] text-sm font-bold text-[var(--accent-fg)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]">
            {pending ? t('customer.reservations.creating') : t('customer.reservations.create')}
          </button>
        </form>
      ) : (
        <p className="rounded-2xl border border-dashed border-[var(--color-border)] p-4 text-sm text-[var(--text-2)]">
          {t('customer.reservations.gate')}
        </p>
      )}

      {reservations === null && <div role="status" aria-label="Загрузка броней" className="h-20 animate-pulse rounded-2xl bg-[var(--color-surface)]" />}
      {reservations !== null && reservations.length === 0 && (
        <p className="py-8 text-center text-[var(--text-2)]">{t('customer.reservations.none')}</p>
      )}
      {reservations !== null && reservations.length > 0 && (
        <ul className="space-y-3">
          {reservations.map((reservation) => (
            <li key={reservation.reservationId} className="rounded-2xl bg-[var(--color-surface)] p-4">
              <div className="flex items-center justify-between">
                <span className="font-bold">{reservation.seatName ?? t('customer.reservations.noSeat')}</span>
                <span className="text-sm text-[var(--text-2)]">{STATE_KEYS[reservation.state] ? t(STATE_KEYS[reservation.state]) : reservation.state}</span>
              </div>
              <p className="mt-1 text-sm text-[var(--text-2)]">
                {formatDateTime(reservation.startsAtUtc)} — {formatDateTime(reservation.endsAtUtc)}
              </p>
              {(reservation.state === 'pending' || reservation.state === 'confirmed') && (
                <button type="button" onClick={() => handleCancel(reservation.reservationId)}
                  className="mt-2 min-h-[44px] text-sm text-red-400 focus-visible:outline-2 focus-visible:outline-[var(--accent)]">
                  {t('customer.reservations.cancel')}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
