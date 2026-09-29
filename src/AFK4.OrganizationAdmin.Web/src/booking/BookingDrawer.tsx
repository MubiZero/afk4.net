import { useState } from 'react';
import { Check, Clock, Copy, TriangleAlert, Wallet, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { formatMinorUnits, formatTime, zoneLabel, type PlayerClientItem } from '../operatorHelpers';
import { formatLocal, localPhoneDigits } from '../phoneFormat';
import { Skeleton } from '../operatorPrimitives';
import { Button, Inspector, StatusBadge, useBlockedReason, type Fact, type RowAction, type StatusTone } from '@afk4/ui/react';
import { useDeferredFlag } from '../useDeferredFlag';
import { PanelSelect } from '../PanelSelect';
import { ClientPicker } from './ClientPicker';
import { DateTimePicker } from './DateTimePicker';
import { bookingDetailActions, bookingStateLabelKey, type BookingItem } from './bookingModel';
import { ReputationCard } from '../players/ReputationCard';
import { RejectPanel } from './RejectPanel';
import type { ReputationController } from '../players/useReputation';

export interface BookingDraft {
  customerName: string;
  phoneNumber: string;       // локальная маска «93 738 00 70»; +992 добавляется при отправке
  playerAccountId: string;   // '' = гость без аккаунта
  clientBalanceMinorUnits: number | null; // баланс выбранного клиента клуба (null = гость)
  clientDebtMinorUnits: number | null;
  startsAt: string;          // datetime-local
  durationMinutes: number;
  seatId: string;
  seatIds: string[];         // непусто = массовая (групповая) бронь на несколько ПК
}

// Куда перенести открытую бронь: места, свободные на её время, — считает сервер.
export interface MoveTargets {
  status: 'loading' | 'ready' | 'failed';
  seats: SeatSummary[];
}

export interface BookingDrawerProps {
  mode: 'detail' | 'create';
  selected: BookingItem | null;
  freeSeats: SeatSummary[];
  moveTargets: MoveTargets;
  allSeats: SeatSummary[];
  draft: BookingDraft;
  busy: boolean;
  canManage: boolean;
  canStartSessions: boolean;
  currencyCode: string;
  conflict: BookingItem | null;      // пересечение с бронью (для детальной подписи)
  seatConflict: boolean;             // место занято бронью ИЛИ сессией — блокирует одиночное создание
  groupConflicts: Set<string>;
  groupSize: number; // сколько активных броней в группе выбранной (detail), 0 если не группа
  searchClients: (query: string) => Promise<PlayerClientItem[]>;
  // Репутация выбранной заявки: спрашивает оркестратор, панель только рисует.
  reputation: ReputationController;
  onClose: () => void;
  onChangeDraft: (patch: Partial<BookingDraft>) => void;
  onCreate: () => void;
  onCreateGroup: () => void;
  onRemoveSeat: (seatId: string) => void;
  onCancelGroup: () => void;
  onStart: () => void;
  /** Отметить приход без запуска сессии. */
  onSeat: () => void;
  onMove: (targetSeatId: string) => void;
  onCancel: () => void;
  onReject: (reasonCode: string, note: string | null) => void;
  onMarkNoShow: () => void;
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

// Телефон клиента в детали брони: он у нас уже есть, поэтому показываем его явно и даём оператору
// скопировать в один клик (десктоп-станция — позвонить из самой панели нельзя, копия в буфер полезнее).
function CopyablePhone({ phone }: { phone: string }) {
  const { t } = useI18n();
  const [copied, setCopied] = useState(false);
  const e164 = `+992${localPhoneDigits(phone)}`;
  const copy = () => {
    void navigator.clipboard?.writeText(e164);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };
  return (
    <button type="button" className="booking-phone-copy" onClick={copy} aria-label={t('op.booking.detail.copyPhone')}>
      <span className="ui-num">+992 {formatLocal(phone)}</span>
      {copied ? <Check size={13} aria-hidden="true" /> : <Copy size={13} aria-hidden="true" />}
    </button>
  );
}

export function BookingDrawer(props: BookingDrawerProps) {
  // Панель отказа живёт в карточке, а не в модалке: причина выбирается там же, где видно, кому и
  // на какое время отказывают.
  const [rejecting, setRejecting] = useState(false);
  // Неявка терминальна и может стоить игроку предоплаты, поэтому спрашивается отдельно — тем же
  // способом, что и отказ: прямо в карточке, где видно, кому и на какое время её ставят.
  const [confirmingNoShow, setConfirmingNoShow] = useState(false);
  const { t } = useI18n();
  const { mode, selected, freeSeats, moveTargets, allSeats, draft, busy, canManage, canStartSessions, currencyCode, conflict, seatConflict, groupConflicts, groupSize } = props;
  // «Уже началась» считается от текущего момента — ровно как на сервере: человек не опоздал, пока
  // его время не наступило.
  const actions = bookingDetailActions(
    selected?.state ?? '',
    selected !== null && selected.startMs <= Date.now(),
    Boolean(selected?.startedSessionId)
  );
  const freeIds = new Set(freeSeats.map((seat) => seat.id));
  // Заявка из приложения может прийти без места: открыть её на карте, посадить или запустить
  // сессию не на что, пока место не выбрано.
  const unassigned = useBlockedReason(mode === 'detail' && selected !== null && !selected.seatId ? t('op.booking.unassignedHint') : null);
  // Прочерк в сером списке не говорил, почему переносить некуда. Пока сервер считает, пустой
  // список — ещё не «мест нет»: подпись о загрузке появляется с задержкой, чтобы быстрый ответ не
  // мигал ею.
  const showMoveLoading = useDeferredFlag(moveTargets.status === 'loading');
  const movePlaceholder = moveTargets.status === 'failed'
    ? t('op.booking.move.failed')
    : moveTargets.status === 'loading'
      ? (showMoveLoading ? t('op.booking.move.loading') : '—')
      : moveTargets.seats.length === 0 ? t('op.booking.move.noFreeSeats') : '—';

  // Массовая бронь: непустой seatIds. Резолвим выбранные места в порядке списка.
  const isGroup = draft.seatIds.length > 0;
  const groupSeats = draft.seatIds
    .map((id) => allSeats.find((seat) => seat.id === id))
    .filter((seat): seat is SeatSummary => seat !== undefined);
  const hasGroupConflict = groupSeats.some((seat) => groupConflicts.has(seat.id));

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
  const close = { label: t('common.close'), disabled: busy, onClose: () => { if (!busy) props.onClose(); } };

  if (mode === 'create') {
    return (
      <Inspector className="booking-inspector" title={t('op.booking.drawer.createTitle')} close={close}>
        {isGroup ? (
          <div className="booking-field">
            <span>{t('op.booking.create.seats', { count: groupSeats.length })}</span>
            <div className="booking-seat-chips" role="list">
              {groupSeats.map((seat) => {
                const conflicted = groupConflicts.has(seat.id);
                const unavailable = !freeIds.has(seat.id);
                return (
                  <span key={seat.id} role="listitem" className={`booking-seat-chip${unavailable ? ' is-unavailable' : ''}${conflicted ? ' is-conflict' : ''}`}>
                    {(unavailable || conflicted) && <TriangleAlert size={11} aria-hidden="true" />}
                    <span>{zoneLabel(seat.zone, t)} · {seat.name}</span>
                    {unavailable && <small>{seat.stateLabel}</small>}
                    <button type="button" aria-label={t('op.booking.group.remove', { seat: seat.name })} disabled={busy} onClick={() => props.onRemoveSeat(seat.id)}><X size={11} /></button>
                  </span>
                );
              })}
            </div>
            {hasGroupConflict && (
              <div className="booking-conflict" role="alert">
                <TriangleAlert size={14} aria-hidden="true" />
                <span>{t('op.booking.group.conflict')}</span>
              </div>
            )}
          </div>
        ) : (
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
        )}
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
        {!conflict && seatConflict && (
          <div className="booking-conflict" role="alert">
            <TriangleAlert size={14} aria-hidden="true" />
            <span>{t('op.booking.conflictSeat')}</span>
          </div>
        )}
        {summaryEnd && (
          <div className="booking-summary">
            <Clock size={14} aria-hidden="true" />
            <div>
              <strong>{isGroup
                ? t('op.booking.create.seats', { count: groupSeats.length })
                : summarySeat ? `${zoneLabel(summarySeat.zone, t)} · ${summarySeat.name}` : t('op.booking.create.seatNone')}</strong>
              <span>{formatTime(summaryStart.toISOString())}–{formatTime(summaryEnd.toISOString())} · {humanizeDuration(draft.durationMinutes)}</span>
            </div>
          </div>
        )}
        {isGroup ? (
          <Button variant="primary" block disabled={!canManage || busy || groupSeats.length === 0 || hasGroupConflict} onClick={props.onCreateGroup}>{t('op.booking.create.submitGroup', { count: groupSeats.length })}</Button>
        ) : (
          <Button variant="primary" block disabled={!canManage || busy || allSeats.length === 0 || !draft.seatId || seatConflict} onClick={props.onCreate}>{t('op.booking.create.submit')}</Button>
        )}
      </Inspector>
    );
  }

  if (selected === null) {
    return (
      <Inspector className="booking-inspector" title={t('op.booking.drawer.detailTitle')} close={close}>
        <Skeleton className="booking-detail-skel" />
      </Inspector>
    );
  }

  const isGroupBooking = Boolean(selected.reservationGroupId) && groupSize > 1;
  const noSeat = !selected.seatId;
  // Главное действие — следующий шаг брони: заявку принять, подтверждённую — посадить за ПК.
  // Без права вести брони кнопок нет вовсе: остаётся только посмотреть место на карте.
  const primary = !canManage
    ? undefined
    : actions.canConfirm
      ? <Button variant="primary" block disabled={busy} onClick={() => props.onConfirm(selected)}>{t(selected.source === 'online' ? 'op.booking.requests.accept' : 'op.booking.actions.confirm')}</Button>
      : actions.canStart && canStartSessions
        ? <Button variant="primary" block disabled={busy || noSeat} aria-describedby={unassigned.describedBy} onClick={props.onStart}>{t('op.booking.actions.startSession')}</Button>
        : undefined;
  // «Отказать» — ответ на заявку, а не отмена: игрок ничего не отменял, деньги ему вернутся
  // целиком. Поэтому он на виду и не красный, а красное «Отменить бронь» — в «⋯».
  const reject = canManage && actions.canReject
    ? <Button disabled={busy} onClick={() => setRejecting(true)}>{t('op.booking.actions.reject')}</Button>
    : null;
  const seat = canManage && actions.canSeat
    ? <Button disabled={busy || noSeat} aria-describedby={unassigned.describedBy} onClick={props.onSeat}>{t('op.booking.actions.seat')}</Button>
    : null;
  const openMap = <Button disabled={busy || noSeat} aria-describedby={unassigned.describedBy} onClick={() => props.onOpenMap(selected.seatId)}>{t('op.booking.actions.openMap')}</Button>;

  const menu: RowAction[] = !canManage ? [] : [
    ...(actions.canMarkNoShow ? [{ id: 'noShow', label: t('op.booking.actions.noShow'), onSelect: () => setConfirmingNoShow(true), danger: true, disabled: busy }] : []),
    { id: 'cancel', label: t('op.booking.action.cancel'), onSelect: props.onCancel, danger: true, disabled: busy },
    ...(isGroupBooking ? [{ id: 'cancelGroup', label: t('op.booking.group.cancelAll'), onSelect: props.onCancelGroup, danger: true, disabled: busy }] : []),
  ];

  const facts: Fact[] = [];
  if (localPhoneDigits(selected.phoneNumber).length > 0) {
    facts.push({ label: t('clients.field.phone'), value: <CopyablePhone phone={selected.phoneNumber} /> });
  }
  if (isGroupBooking) {
    facts.push({ label: t('op.booking.detail.group'), value: t('op.booking.group.seats', { count: groupSize }) });
  }
  if (selected.state === 'pending' && selected.respondByMs !== null) {
    facts.push({ label: t('op.booking.detail.respondBy'), value: formatTime(new Date(selected.respondByMs).toISOString()) });
  }
  facts.push({ label: t('op.booking.detail.source'), value: selected.source === 'online' ? t('op.booking.source.online') : t('op.booking.source.operator') });
  // Комментарий — только когда он есть: «без комментария» строкой было шумом у каждой брони.
  if (selected.note) {
    facts.push({ label: t('op.booking.detail.comment'), value: selected.note });
  }

  return (
    <Inspector
      className="booking-inspector"
      title={selected.customerName}
      status={<StatusBadge tone={STATUS_TONE[selected.tone] ?? 'neutral'}>{t(bookingStateLabelKey(selected.state))}</StatusBadge>}
      subtitle={[selected.zoneName ? zoneLabel(selected.zoneName, t) : '', selected.seatName, t('op.booking.durationMin', { count: selected.durationMinutes })].filter(Boolean).join(' · ')}
      menu={{ label: t('op.booking.menu.open'), actions: menu }}
      close={close}
      figure={{
        label: t('op.booking.detail.time'),
        value: `${formatTime(new Date(selected.startMs).toISOString())}–${formatTime(new Date(selected.endMs).toISOString())}`,
      }}
    >
      <Inspector.Actions
        primary={primary}
        hint={unassigned.hint}
        secondary={reject !== null && seat !== null ? [reject, seat, openMap] : reject !== null ? [reject, openMap] : seat !== null ? [seat, openMap] : [openMap]}
      />

      {confirmingNoShow && actions.canMarkNoShow && (
        <div className="booking-reject" role="group" aria-label={t('op.booking.noShow.confirmTitle')}>
          <p className="booking-reject-title">{t('op.booking.noShow.confirmTitle')}</p>
          <p className="booking-reject-hint">{t('op.booking.noShow.confirmBody')}</p>
          <div className="booking-reject-actions">
            <Button variant="danger" disabled={busy} onClick={() => { setConfirmingNoShow(false); props.onMarkNoShow(); }}>
              {t('op.booking.noShow.confirmAction')}
            </Button>
            <Button disabled={busy} onClick={() => setConfirmingNoShow(false)}>{t('op.booking.reject.back')}</Button>
          </div>
        </div>
      )}

      {rejecting && actions.canReject && (
        <RejectPanel
          busy={busy}
          onSend={(reasonCode, note) => {
            setRejecting(false);
            props.onReject(reasonCode, note);
          }}
          onDismiss={() => setRejecting(false)}
        />
      )}

      {canManage && (
        <div className="booking-field">
          <span>{t('op.booking.move.seat')}</span>
          <PanelSelect
            ariaLabel={t('op.booking.move.seat')}
            value=""
            placeholder={movePlaceholder}
            disabled={busy || moveTargets.status !== 'ready' || moveTargets.seats.length === 0}
            options={groupSeatsByZone(moveTargets.seats).map((seat) => ({
              value: seat.id,
              label: `${zoneLabel(seat.zone, t)} · ${seat.name}`
            }))}
            onChange={(seatId) => { if (seatId) props.onMove(seatId); }}
          />
        </div>
      )}

      <Inspector.Facts items={facts} />

      <ReputationCard controller={props.reputation} />
    </Inspector>
  );
}

// Тон статуса брони — словарь кита, в тон блоку на ленте: заявка ждёт (жёлтый), подтверждённая
// (зелёный), онлайн (акцент); посаженная и отменённая — спокойные.
const STATUS_TONE: Partial<Record<BookingItem['tone'], StatusTone>> = {
  pending: 'warning',
  confirmed: 'success',
  online: 'accent',
};
