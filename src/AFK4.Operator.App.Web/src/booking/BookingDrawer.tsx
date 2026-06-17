import { MonitorCheck, Plus, Square, UserRoundPlus, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import type { Feedback } from '../operatorTypes';
import { formatTime, zoneLabel } from '../operatorHelpers';
import { FeedbackNotice, Skeleton } from '../operatorPrimitives';
import type { BookingItem } from './bookingModel';

export interface BookingDraft {
  customerName: string;
  phoneNumber: string;
  startsAt: string;       // datetime-local
  durationMinutes: number;
  seatId: string;
}

export interface BookingDrawerProps {
  mode: 'detail' | 'create';
  selected: BookingItem | null;
  freeSeats: SeatSummary[];
  draft: BookingDraft;
  feedback: Feedback;
  busy: boolean;
  canManage: boolean;
  onClose: () => void;
  onChangeDraft: (patch: Partial<BookingDraft>) => void;
  onCreate: () => void;
  onSeat: () => void;
  onMove: (targetSeatId: string) => void;
  onCancel: () => void;
  onConfirm: (item: BookingItem) => void;
  onOpenMap: (seatId: string) => void;
}

export function BookingDrawer(props: BookingDrawerProps) {
  const { t } = useI18n();
  const { mode, selected, freeSeats, draft, feedback, busy, canManage } = props;
  const title = mode === 'create' ? t('op.booking.drawer.createTitle') : t('op.booking.drawer.detailTitle');

  return (
    <aside className="booking-drawer" role="dialog" aria-label={title}>
      <header className="booking-drawer-head">
        <strong>{title}</strong>
        <button type="button" className="booking-drawer-close" aria-label={t('common.cancel')} onClick={props.onClose}><X size={16} /></button>
      </header>

      {mode === 'create' ? (
        <div className="booking-drawer-body">
          <label className="booking-field">{t('op.booking.create.seat')}
            <select value={draft.seatId} disabled={busy || freeSeats.length === 0} onChange={(e) => props.onChangeDraft({ seatId: e.target.value })}>
              {freeSeats.length === 0 && <option value="">{t('op.booking.create.seatNone')}</option>}
              {freeSeats.map((seat) => (
                <option key={seat.id} value={seat.id}>{zoneLabel(seat.zone, t)} · {seat.name}</option>
              ))}
            </select>
          </label>
          <label className="booking-field">{t('op.booking.client')}
            <input value={draft.customerName} disabled={busy} onChange={(e) => props.onChangeDraft({ customerName: e.target.value })} />
          </label>
          <label className="booking-field">{t('clients.field.phone')}
            <input value={draft.phoneNumber} disabled={busy} onChange={(e) => props.onChangeDraft({ phoneNumber: e.target.value })} />
          </label>
          <label className="booking-field">{t('op.booking.create.start')}
            <input type="datetime-local" value={draft.startsAt} disabled={busy} onChange={(e) => props.onChangeDraft({ startsAt: e.target.value })} />
          </label>
          <label className="booking-field">{t('op.booking.create.duration')}
            <input type="number" min={15} step={15} value={draft.durationMinutes} disabled={busy} onChange={(e) => props.onChangeDraft({ durationMinutes: Number(e.target.value) || 60 })} />
          </label>
          <FeedbackNotice feedback={feedback} />
          <button type="button" className="booking-primary-action" disabled={!canManage || busy || freeSeats.length === 0 || !draft.seatId} onClick={props.onCreate}>{t('op.booking.create.submit')}</button>
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

          <label className="booking-field">{t('op.booking.move.seat')}
            <select value="" disabled={!canManage || busy || freeSeats.length === 0}
              onChange={(e) => { if (e.target.value) props.onMove(e.target.value); }}>
              <option value="">{freeSeats.length === 0 ? t('op.booking.create.seatNone') : '—'}</option>
              {freeSeats.filter((s) => s.id !== selected.seatId).map((seat) => (
                <option key={seat.id} value={seat.id}>{zoneLabel(seat.zone, t)} · {seat.name}</option>
              ))}
            </select>
          </label>

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
