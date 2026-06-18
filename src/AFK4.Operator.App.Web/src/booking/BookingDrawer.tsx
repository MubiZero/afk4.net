import { Clock, MonitorCheck, Plus, Square, TriangleAlert, UserRoundPlus, Wallet, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import type { Feedback } from '../operatorTypes';
import { formatMinorUnits, formatTime, zoneLabel, type PlayerClientItem } from '../operatorHelpers';
import { formatLocal } from '../phoneFormat';
import { FeedbackNotice, Skeleton } from '../operatorPrimitives';
import { PanelSelect } from '../PanelSelect';
import { ClientPicker } from './ClientPicker';
import { DateTimePicker } from './DateTimePicker';
import type { BookingItem } from './bookingModel';

export interface BookingDraft {
  customerName: string;
  phoneNumber: string;       // локальная маска «93 738 00 70»; +992 добавляется при отправке
  playerAccountId: string;   // '' = гость без аккаунта
  clientBalanceMinorUnits: number | null; // баланс выбранного клиента клуба (null = гость)
  clientDebtMinorUnits: number | null;
  startsAt: string;          // datetime-local
  durationMinutes: number;
  seatId: string;
}

export interface BookingDrawerProps {
  mode: 'detail' | 'create';
  selected: BookingItem | null;
  freeSeats: SeatSummary[];
  allSeats: SeatSummary[];
  draft: BookingDraft;
  feedback: Feedback;
  busy: boolean;
  canManage: boolean;
  currencyCode: string;
  conflict: BookingItem | null;
  searchClients: (query: string) => Promise<PlayerClientItem[]>;
  onClose: () => void;
  onChangeDraft: (patch: Partial<BookingDraft>) => void;
  onCreate: () => void;
  onSeat: () => void;
  onMove: (targetSeatId: string) => void;
  onCancel: () => void;
  onConfirm: (item: BookingItem) => void;
  onOpenMap: (seatId: string) => void;
}

// Сырой список мест отсортирован глобально (по колонкам зала), из-за чего в дропдауне залы
// перемешаны. Группируем по залу в порядке первой встречи (как таймлайн), внутри зала порядок
// мест сохраняется — он уже идёт по очереди ПК.
function groupSeatsByZone(seats: SeatSummary[]): SeatSummary[] {
  const zoneOrder: string[] = [];
  const byZone = new Map<string, SeatSummary[]>();
  for (const seat of seats) {
    const bucket = byZone.get(seat.zone);
    if (bucket) {
      bucket.push(seat);
    } else {
      byZone.set(seat.zone, [seat]);
      zoneOrder.push(seat.zone);
    }
  }
  return zoneOrder.flatMap((zone) => byZone.get(zone) ?? []);
}

export function BookingDrawer(props: BookingDrawerProps) {
  const { t } = useI18n();
  const { mode, selected, freeSeats, allSeats, draft, feedback, busy, canManage, currencyCode, conflict } = props;
  const title = mode === 'create' ? t('op.booking.drawer.createTitle') : t('op.booking.drawer.detailTitle');
  const freeIds = new Set(freeSeats.map((seat) => seat.id));

  // Человекочитаемая длительность: «2 часа 30 минут» (часы опускаем при <60 мин).
  const humanizeDuration = (totalMinutes: number): string => {
    const safe = Math.max(0, Math.round(totalMinutes));
    const hours = Math.floor(safe / 60);
    const minutes = safe % 60;
    const parts: string[] = [];
    if (hours > 0) parts.push(t('op.booking.durationHourFull', { count: hours }));
    if (minutes > 0 || hours === 0) parts.push(t('op.booking.durationMinFull', { count: minutes }));
    return parts.join(' ');
  };

  // Сводка создаваемой брони: место · до HH:MM · длительность.
  const summarySeat = allSeats.find((seat) => seat.id === draft.seatId) ?? null;
  const summaryStart = new Date(draft.startsAt);
  const summaryEnd = Number.isNaN(summaryStart.getTime())
    ? null
    : new Date(summaryStart.getTime() + Math.max(15, draft.durationMinutes) * 60_000);
  const hasBalance = draft.clientBalanceMinorUnits !== null;
  const inDebt = (draft.clientDebtMinorUnits ?? 0) > 0;
  const lowBalance = (draft.clientBalanceMinorUnits ?? 0) <= 0;

  return (
    <aside className="booking-drawer" role="dialog" aria-label={title}>
      <header className="booking-drawer-head">
        <strong>{title}</strong>
        <button type="button" className="booking-drawer-close" aria-label={t('common.cancel')} onClick={props.onClose}><X size={16} /></button>
      </header>

      {mode === 'create' ? (
        <div className="booking-drawer-body">
          <div className="booking-field">
            <span>{t('op.booking.create.seat')}</span>
            <PanelSelect
              ariaLabel={t('op.booking.create.seat')}
              value={draft.seatId}
              placeholder={t('op.booking.create.seatNone')}
              disabled={busy || allSeats.length === 0}
              options={groupSeatsByZone(allSeats).map((seat) => ({
                value: seat.id,
                label: `${zoneLabel(seat.zone, t)} · ${seat.name}${freeIds.has(seat.id) ? '' : ` · ${seat.stateLabel}`}`
              }))}
              onChange={(seatId) => props.onChangeDraft({ seatId })}
            />
          </div>
          <div className="booking-field">
            <span>{t('op.booking.client')}</span>
            <ClientPicker
              value={draft.customerName}
              linked={Boolean(draft.playerAccountId)}
              disabled={busy}
              search={props.searchClients}
              onQueryChange={(name) => props.onChangeDraft({ customerName: name, playerAccountId: '', clientBalanceMinorUnits: null, clientDebtMinorUnits: null })}
              onPick={(pick) => props.onChangeDraft({ customerName: pick.name, phoneNumber: formatLocal(pick.phoneNumber), playerAccountId: pick.playerAccountId, clientBalanceMinorUnits: pick.balanceMinorUnits, clientDebtMinorUnits: pick.debtMinorUnits })}
              onClear={() => props.onChangeDraft({ customerName: '', phoneNumber: '', playerAccountId: '', clientBalanceMinorUnits: null, clientDebtMinorUnits: null })}
            />
            {hasBalance && (
              <span className={`booking-balance${inDebt ? ' is-debt' : lowBalance ? ' is-low' : ''}`}>
                <Wallet size={12} aria-hidden="true" />
                {inDebt
                  ? t('op.booking.client.debt', { amount: formatMinorUnits(draft.clientDebtMinorUnits ?? 0, currencyCode) })
                  : t('op.booking.client.balance', { amount: formatMinorUnits(draft.clientBalanceMinorUnits ?? 0, currencyCode) })}
              </span>
            )}
          </div>
          <div className="booking-field">
            <span>{t('clients.field.phone')}</span>
            <div className="booking-phone-field">
              <span className="booking-phone-prefix" aria-hidden="true">+992</span>
              <input
                type="tel"
                inputMode="tel"
                aria-label={t('clients.field.phone')}
                value={draft.phoneNumber}
                disabled={busy}
                placeholder="93 738 00 70"
                onChange={(e) => props.onChangeDraft({ phoneNumber: formatLocal(e.currentTarget.value) })}
              />
            </div>
          </div>
          <div className="booking-field">
            <span>{t('op.booking.create.start')}</span>
            <DateTimePicker
              value={draft.startsAt}
              disabled={busy}
              ariaLabel={t('op.booking.create.start')}
              onChange={(next) => props.onChangeDraft({ startsAt: next })}
            />
          </div>
          <div className="booking-field">
            <span className="booking-field-head">
              {t('op.booking.create.duration')}
              <em className="booking-duration-human">{humanizeDuration(draft.durationMinutes)}</em>
            </span>
            <div className="booking-duration-field">
              <input type="number" min={15} step={15} value={draft.durationMinutes} disabled={busy} onChange={(e) => props.onChangeDraft({ durationMinutes: Number(e.target.value) || 60 })} />
              <span className="booking-duration-suffix" aria-hidden="true">{t('op.booking.durationUnit')}</span>
            </div>
          </div>
          <div className="booking-duration-quick" role="group" aria-label={t('op.booking.create.duration')}>
            {[30, 60, 90, 120].map((minutes) => (
              <button
                key={minutes}
                type="button"
                className={draft.durationMinutes === minutes ? 'active' : undefined}
                disabled={busy}
                onClick={() => props.onChangeDraft({ durationMinutes: minutes })}
              >{t('op.booking.durationMin', { count: minutes })}</button>
            ))}
          </div>
          {conflict && (
            <div className="booking-conflict" role="alert">
              <TriangleAlert size={14} aria-hidden="true" />
              <span>{t('op.booking.conflict', {
                from: formatTime(new Date(conflict.startMs).toISOString()),
                to: formatTime(new Date(conflict.endMs).toISOString()),
                client: conflict.customerName
              })}</span>
            </div>
          )}
          {summaryEnd && (
            <div className="booking-summary">
              <Clock size={14} aria-hidden="true" />
              <div>
                <strong>{summarySeat ? `${zoneLabel(summarySeat.zone, t)} · ${summarySeat.name}` : t('op.booking.create.seatNone')}</strong>
                <span>{formatTime(summaryStart.toISOString())}–{formatTime(summaryEnd.toISOString())} · {humanizeDuration(draft.durationMinutes)}</span>
              </div>
            </div>
          )}
          <FeedbackNotice feedback={feedback} />
          <button type="button" className="booking-primary-action" disabled={!canManage || busy || allSeats.length === 0 || !draft.seatId} onClick={props.onCreate}><Plus size={15} />{t('op.booking.create.submit')}</button>
        </div>
      ) : selected ? (
        <div className="booking-drawer-body">
          <div className={`booking-status-card ${selected.tone}`}>
            <span>{selected.state}</span>
            <strong>{formatTime(new Date(selected.startMs).toISOString())}</strong>
            <em>{selected.seatName ? `${zoneLabel(selected.zoneName, t)} · ${selected.seatName}` : zoneLabel(selected.zoneName, t)} · {t('op.booking.durationMin', { count: selected.durationMinutes })}</em>
          </div>

          <div className="booking-action-grid">
            <button type="button" disabled={!selected.seatId || busy} onClick={() => props.onOpenMap(selected.seatId)}><MonitorCheck size={15} />{t('op.booking.actions.openMap')}</button>
            <button type="button" disabled={!canManage || busy || !selected.seatId} onClick={props.onSeat}><UserRoundPlus size={15} />{t('op.booking.actions.seat')}</button>
            {selected.source === 'online' && selected.state === 'pending' && (
              <button type="button" disabled={!canManage || busy} onClick={() => props.onConfirm(selected)}><Plus size={15} />{t('op.booking.requests.accept')}</button>
            )}
            <button type="button" className="danger" disabled={!canManage || busy} onClick={props.onCancel}><Square size={15} />{t('op.booking.actions.cancel')}</button>
          </div>

          <div className="booking-field">
            <span>{t('op.booking.move.seat')}</span>
            <PanelSelect
              ariaLabel={t('op.booking.move.seat')}
              value=""
              placeholder="—"
              disabled={!canManage || busy || freeSeats.filter((s) => s.id !== selected.seatId).length === 0}
              options={groupSeatsByZone(freeSeats.filter((s) => s.id !== selected.seatId)).map((seat) => ({
                value: seat.id,
                label: `${zoneLabel(seat.zone, t)} · ${seat.name}`
              }))}
              onChange={(seatId) => { if (seatId) props.onMove(seatId); }}
            />
          </div>

          <FeedbackNotice feedback={feedback} />

          <div className="booking-detail-list">
            <div><span>{t('op.booking.client')}</span><strong>{selected.customerName}</strong></div>
            <div><span>{t('op.booking.detail.comment')}</span><strong>{selected.note || t('op.booking.noComment')}</strong></div>
            <div><span>{t('op.booking.detail.source')}</span><strong>{selected.source === 'online' ? t('op.booking.source.online') : t('op.booking.source.operator')}</strong></div>
          </div>
        </div>
      ) : (
        <div className="booking-drawer-body">
          <Skeleton className="booking-detail-skel" />
        </div>
      )}
    </aside>
  );
}
