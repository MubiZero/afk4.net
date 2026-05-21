import {
  AlertTriangle,
  ArrowRightLeft,
  Banknote,
  CalendarClock,
  CircleDollarSign,
  Clock3,
  LockKeyhole,
  Maximize2,
  Minus,
  MonitorCheck,
  Plus,
  ReceiptText,
  Search,
  ShieldAlert,
  Square,
  TimerReset,
  UserRoundPlus,
  Wifi,
  Wrench,
  X
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useEffect, useState, type CSSProperties, type MouseEvent, type ReactNode } from 'react';
import { postHostWindowCommand } from './hostBridge';
import { getOperatorConfig } from './operatorConfig';
import { navItems, seats, signals, type SeatSummary, type SeatTone } from './operatorData';

type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'players' | 'payments' | 'logs' | 'settings';
type DashboardPeriod = 'today' | 'week' | 'month' | 'custom';
type FeedbackState = 'idle' | 'pending' | 'confirmed' | 'failed';
type Feedback = { label: string; state: FeedbackState };

const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'logs', 'settings'];

const toneLabels: Record<SeatTone, string> = {
  ready: 'Готов',
  active: 'Активно',
  pending: 'Команда',
  warning: 'Внимание',
  blocking: 'Блокер',
  offline: 'Офлайн',
  service: 'Сервис'
};

const problemTones = new Set<SeatTone>(['pending', 'warning', 'blocking', 'offline', 'service']);
const emptyFeedback: Feedback = { label: '', state: 'idle' };

function handleWindowDragStart(event: MouseEvent<HTMLElement>) {
  if (event.button !== 0) {
    return;
  }

  const target = event.target as HTMLElement;
  if (target.closest('button, input, .command-search')) {
    return;
  }

  postHostWindowCommand('drag');
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

function countPeriodDays(from: string, to: string) {
  const fromDate = new Date(`${from}T00:00:00`);
  const toDate = new Date(`${to}T00:00:00`);

  if (Number.isNaN(fromDate.getTime()) || Number.isNaN(toDate.getTime()) || toDate < fromDate) {
    return 1;
  }

  return Math.max(1, Math.round((toDate.getTime() - fromDate.getTime()) / 86_400_000) + 1);
}

function formatCompactNumber(value: number) {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(value >= 10000 ? 0 : 1)}k`;
  }

  return String(value);
}

function pluralRu(value: number, forms: [string, string, string]) {
  const absolute = Math.abs(value) % 100;
  const last = absolute % 10;

  if (absolute > 10 && absolute < 20) {
    return forms[2];
  }

  if (last === 1) {
    return forms[0];
  }

  if (last >= 2 && last <= 4) {
    return forms[1];
  }

  return forms[2];
}

function parseMoney(value: string) {
  const parsed = Number(value.replace(/[^\d-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

function triggerFeedback(
  setFeedback: (feedback: Feedback) => void,
  label: string,
  finalState: Exclude<FeedbackState, 'idle' | 'pending'> = 'confirmed'
) {
  setFeedback({ label, state: 'pending' });
  window.setTimeout(() => setFeedback({ label, state: finalState }), 620);
}

function feedbackText(feedback: Feedback) {
  if (feedback.state === 'pending') {
    return `${feedback.label}: ждём подтверждение платформы`;
  }

  if (feedback.state === 'failed') {
    return `${feedback.label}: нужен повтор или проверка`;
  }

  if (feedback.state === 'confirmed') {
    return `${feedback.label}: подтверждено`;
  }

  return '';
}

function FeedbackNotice({ feedback }: { feedback: Feedback }) {
  if (feedback.state === 'idle') {
    return null;
  }

  return (
    <div className={`feedback-notice ${feedback.state}`} role="status" aria-live="polite">
      {feedbackText(feedback)}
    </div>
  );
}

function useAnimatedNumber(value: number, duration = 360) {
  const [displayValue, setDisplayValue] = useState(value);

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.requestAnimationFrame !== 'function') {
      setDisplayValue(value);
      return undefined;
    }

    const startValue = displayValue;
    const difference = value - startValue;

    if (difference === 0) {
      return undefined;
    }

    const startedAt = window.performance.now();
    let frame = 0;

    const tick = (now: number) => {
      const progress = Math.min(1, (now - startedAt) / duration);
      const eased = 1 - Math.pow(1 - progress, 3);
      setDisplayValue(Math.round(startValue + difference * eased));

      if (progress < 1) {
        frame = window.requestAnimationFrame(tick);
      }
    };

    frame = window.requestAnimationFrame(tick);
    return () => window.cancelAnimationFrame(frame);
  }, [value]);

  return displayValue;
}

function AnimatedNumber({
  value,
  formatter = (nextValue: number) => String(nextValue)
}: {
  value: number;
  formatter?: (nextValue: number) => string;
}) {
  return <>{formatter(useAnimatedNumber(value))}</>;
}

function countByTone(tone: SeatTone): number {
  return seats.filter((seat) => seat.tone === tone).length;
}

function countProblems(): number {
  return seats.filter((seat) => problemTones.has(seat.tone)).length;
}

function zoneClass(zone: string): string {
  if (zone.includes('VIP')) {
    return 'zone-vip';
  }

  if (zone.includes('Bootcamp')) {
    return 'zone-bootcamp';
  }

  if (zone.includes('C')) {
    return 'zone-c';
  }

  if (zone.includes('B')) {
    return 'zone-b';
  }

  return 'zone-a';
}

function SeatTile({
  seat,
  selected,
  onSelect
}: {
  seat: SeatSummary;
  selected?: boolean;
  onSelect: () => void;
}) {
  return (
    <article
      className={`seat-tile ${zoneClass(seat.zone)} state-${seat.tone}${selected ? ' selected' : ''}`}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSelect();
        }
      }}
      role="button"
      tabIndex={0}
    >
      <header className="seat-head">
        <div>
          <strong>{seat.name}</strong>
          <span>{seat.zone}</span>
        </div>
        <span className="state-chip">{seat.stateLabel}</span>
      </header>
      <div className="seat-main">
        <span>{seat.player}</span>
        <span>{seat.app}</span>
      </div>
      <footer>
        <strong>{seat.remaining}</strong>
        <span>{seat.command}</span>
      </footer>
    </article>
  );
}

function StateFlag({ label, value, critical }: { label: string; value: string; critical?: boolean }) {
  return (
    <section className={`state-flag${critical ? ' critical' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </section>
  );
}

function billingLabel(value: string) {
  const normalized = value.toLowerCase();

  if (normalized.includes('wallet')) {
    return 'Депозит';
  }

  if (normalized.includes('package')) {
    return 'Пакет';
  }

  if (normalized.includes('postpaid')) {
    return 'Постоплата';
  }

  if (normalized.includes('guest')) {
    return 'Гость';
  }

  return 'Не задан';
}

function commandLabel(command: string) {
  if (command.includes('Lease fresh')) {
    return 'Сессия подтверждена';
  }

  if (command.includes('Unlock pending')) {
    return 'Разблокировка в процессе';
  }

  if (command.includes('Payment check')) {
    return 'Проверить оплату';
  }

  if (command.includes('No route')) {
    return 'Нет связи с ПК';
  }

  if (command.includes('Idle')) {
    return 'Команд нет';
  }

  return command;
}

function deviceStatusLabel(device: string) {
  return device
    .replace('Online', 'Онлайн')
    .replace('Offline', 'Нет связи')
    .replace('unlocked', 'разблокирован')
    .replace('locked state unknown', 'статус блокировки неизвестен')
    .replace('locked', 'заблокирован');
}

function mapSeatStatus(seat: SeatSummary) {
  if (seat.tone === 'active') {
    return {
      label: 'Сессия активна',
      value: seat.remaining
    };
  }

  if (seat.tone === 'ready') {
    return {
      label: 'Свободен',
      value: 'готов к старту'
    };
  }

  if (seat.tone === 'pending') {
    return {
      label: 'Команда в пути',
      value: commandLabel(seat.command)
    };
  }

  if (seat.tone === 'warning') {
    return {
      label: 'Требует проверки',
      value: commandLabel(seat.command)
    };
  }

  if (seat.tone === 'offline') {
    return {
      label: 'Нет связи',
      value: commandLabel(seat.command)
    };
  }

  return {
    label: 'Техрежим',
    value: commandLabel(seat.command)
  };
}

function MapWorkspace({
  currencyCode,
  selectedSeatId,
  onSelectSeat
}: {
  currencyCode: string;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
}) {
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);

  return (
    <main className="floor-workspace">
      <section className="map-toolbar">
        <div>
          <span>Карта</span>
          <h1>AFK4 Dushanbe · зал A</h1>
        </div>
        <div className="screen-actions">
          <button type="button" className="map-tool-action" onClick={() => triggerFeedback(setFeedback, 'Техрежим')}>
            <Wrench size={14} />Техрежим
          </button>
        </div>
      </section>

      <section className="state-strip" aria-label="Сводка">
        <StateFlag label="Сессии" value={String(countByTone('active'))} />
        <StateFlag label="Свободно" value={String(countByTone('ready'))} />
        <StateFlag label="Команды" value={String(countByTone('pending'))} critical={countByTone('pending') > 0} />
        <StateFlag label="Нет связи" value={String(countByTone('offline'))} critical={countByTone('offline') > 0} />
        <StateFlag label="Проблемы" value={String(countProblems())} critical={countProblems() > 0} />
        <StateFlag label="Касса" value={`4 820 ${currencyCode}`} />
      </section>
      <FeedbackNotice feedback={feedback} />

      <section className="map-board" aria-label="ПК зала">
        <div className="seat-grid">
          {seats.map((seat) => (
            <SeatTile
              key={seat.id}
              seat={seat}
              selected={seat.id === selectedSeatId}
              onSelect={() => onSelectSeat(seat.id)}
            />
          ))}
        </div>
      </section>
    </main>
  );
}

function DashboardWorkspace({ currencyCode }: { currencyCode: string }) {
  const today = new Date();
  const todayInput = toDateInputValue(today);
  const weekStartInput = toDateInputValue(addDays(today, -6));
  const monthStartInput = toDateInputValue(addDays(today, -29));
  const [period, setPeriod] = useState<DashboardPeriod>('today');
  const [customRange, setCustomRange] = useState({ from: weekStartInput, to: todayInput });
  const [selectedFocusIndex, setSelectedFocusIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);

  const presetRanges = {
    today: { from: todayInput, to: todayInput, label: 'сегодня', metricLabel: 'сегодня' },
    week: { from: weekStartInput, to: todayInput, label: 'за неделю', metricLabel: 'неделю' },
    month: { from: monthStartInput, to: todayInput, label: 'за месяц', metricLabel: 'месяц' }
  };

  const activeRange = period === 'custom'
    ? { ...customRange, label: 'за выбранный период', metricLabel: 'выбранный период' }
    : presetRanges[period];
  const activeDays = countPeriodDays(activeRange.from, activeRange.to);
  const moneyFormatter = new Intl.NumberFormat('ru-RU');
  const cashTotal = 4_820 * activeDays;
  const cashTarget = 6_000 * activeDays;
  const cashPercent = Math.min(100, Math.round((cashTotal / cashTarget) * 100));
  const attentionCount = 4 * activeDays;
  const bookingUsed = 5 * activeDays;
  const bookingSlots = 8 * activeDays;
  const posChecks = 2 * activeDays;
  const newClients = Math.max(1, Math.round(1.4 * activeDays));
  const averageActivePcs = activeDays === 1 ? 9 : activeDays <= 7 ? 11 : 12;
  const activePeriodLabel = period === 'custom' ? `${activeDays} дн.` : activeRange.metricLabel;
  const periodDaysShort = `${activeDays} дн.`;
  const exportLabel = `${activeRange.from} - ${activeRange.to}`;
  const updateCustomRange = (field: 'from' | 'to', value: string) => {
    setCustomRange((range) => ({ ...range, [field]: value }));
    setPeriod('custom');
  };

  const focusItems = [
    ['warning', 'PC-04', 'Долг 86 TJS · 12 мин до лимита', 'Связаться с игроком или перевести долг в оплату'],
    ['pending', 'PC-03', 'Разблокировка запускается · 1 мин', 'Дождаться ответа ПК, затем проверить старт сессии'],
    ['service', 'PC-05', 'Нет связи · 7 мин', 'Проверить ПК после восстановления связи']
  ];
  const selectedFocus = focusItems[selectedFocusIndex];

  const pulseItems = [
    { label: 'Касса', value: `${moneyFormatter.format(cashTotal)} ${currencyCode}`, detail: `из ${moneyFormatter.format(cashTarget)} ${currencyCode}`, chartValue: cashPercent, chartLabel: <><AnimatedNumber value={cashPercent} />%</>, chartSubLabel: formatCompactNumber(cashTotal), tone: 'cash', icon: Banknote },
    { label: 'Активные ПК', value: `${averageActivePcs} / 24`, detail: `среднее за ${activePeriodLabel}`, chartValue: Math.round((averageActivePcs / 24) * 100), chartLabel: <><AnimatedNumber value={averageActivePcs} />/24</>, chartSubLabel: 'средн.', tone: 'devices', icon: MonitorCheck },
    { label: 'Внимание', value: String(attentionCount), detail: `${pluralRu(attentionCount, ['сигнал', 'сигнала', 'сигналов'])} за ${activePeriodLabel}`, chartValue: Math.min(100, Math.round((attentionCount / (24 * activeDays)) * 100)), chartLabel: <AnimatedNumber value={attentionCount} />, chartSubLabel: 'сигн.', tone: 'attention', icon: ShieldAlert },
    { label: 'Брони', value: `${bookingUsed} / ${bookingSlots}`, detail: `слоты за ${activePeriodLabel}`, chartValue: Math.min(100, Math.round((bookingUsed / bookingSlots) * 100)), chartLabel: <><AnimatedNumber value={bookingUsed} />/{bookingSlots}</>, chartSubLabel: 'слоты', tone: 'booking', icon: CalendarClock }
  ];

  const controlCards: Array<[string, string, string, LucideIcon]> = [
    ['Карта', '24 ПК', `${attentionCount} ${pluralRu(attentionCount, ['сигнал', 'сигнала', 'сигналов'])}`, MonitorCheck],
    ['POS', `${posChecks} ${pluralRu(posChecks, ['чек', 'чека', 'чеков'])}`, `за ${activePeriodLabel}`, ReceiptText],
    ['Депозит', `+${moneyFormatter.format(740 * activeDays)} ${currencyCode}`, `за ${activePeriodLabel}`, CircleDollarSign],
    ['Клиент', `${newClients} ${pluralRu(newClients, ['новый', 'новых', 'новых'])}`, `за ${activePeriodLabel}`, UserRoundPlus]
  ];

  return (
    <main className="workspace-screen dashboard-screen">
      <section className="screen-head dashboard-head">
        <div>
          <span>Dashboard</span>
          <h1>Что требует внимания · {activeRange.label}</h1>
        </div>
        <div className="filter-row dashboard-period-filter" aria-label="Период данных дашборда">
          <div className="period-segment">
            <button type="button" className={period === 'today' ? 'active' : undefined} onClick={() => setPeriod('today')}>Сегодня</button>
            <button type="button" className={period === 'week' ? 'active' : undefined} onClick={() => setPeriod('week')}>Неделя</button>
            <button type="button" className={period === 'month' ? 'active' : undefined} onClick={() => setPeriod('month')}>Месяц</button>
          </div>
          <div className={`date-range-control ${period === 'custom' ? 'active' : ''}`}>
            <label>
              <span>с</span>
              <input
                type="date"
                aria-label="Начало периода"
                value={customRange.from}
                onChange={(event) => updateCustomRange('from', event.currentTarget.value)}
                onInput={(event) => updateCustomRange('from', event.currentTarget.value)}
                onFocus={() => setPeriod('custom')}
              />
            </label>
            <label>
              <span>по</span>
              <input
                type="date"
                aria-label="Конец периода"
                value={customRange.to}
                onChange={(event) => updateCustomRange('to', event.currentTarget.value)}
                onInput={(event) => updateCustomRange('to', event.currentTarget.value)}
                onFocus={() => setPeriod('custom')}
              />
            </label>
            <span className="date-range-days" aria-label={`Длина периода: ${periodDaysShort}`}>{periodDaysShort}</span>
          </div>
          <button type="button" className="export-button" aria-label={`Экспорт дашборда за ${exportLabel}`}>
            Экспорт
          </button>
        </div>
      </section>

      <section className="dashboard-layout">
        <article className="dashboard-now-panel">
          <header className="dashboard-panel-title">
            <span>Главный фокус</span>
            <strong>PC-11 · блокировка не подтверждена</strong>
          </header>
          <p>ПК не подтвердил блокировку после завершения сессии. Это единственный критический пункт на смене прямо сейчас.</p>
          <div className="dashboard-now-meta">
            <span><AlertTriangle size={15} /> Блокер</span>
            <span>сейчас</span>
            <span>Команда принята · ПК не ответил</span>
          </div>
          <div className="dashboard-now-actions">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Разобрать PC-11')}><AlertTriangle size={15} /> Разобрать</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Техрежим PC-11')}><Wrench size={15} /> Техрежим</button>
          </div>
          <FeedbackNotice feedback={feedback} />
        </article>

        <section className="dashboard-secondary-panel">
          <header className="dashboard-panel-title">
            <span>Дальше по очереди</span>
            <strong>разобрать после критичного</strong>
          </header>
          <div className="focus-list">
            {focusItems.map(([tone, target, title, detail], index) => (
              <button
                key={`${target}-${title}`}
                type="button"
                className={`focus-row ${tone}${index === selectedFocusIndex ? ' active' : ''}`}
                onClick={() => setSelectedFocusIndex(index)}
              >
                <div>
                  <span>{target}</span>
                  <strong>{title}</strong>
                  <em>{detail}</em>
                </div>
              </button>
            ))}
          </div>
          <div className="dashboard-selected-signal">
            <span>{selectedFocus[1]}</span>
            <strong>{selectedFocus[3]}</strong>
          </div>
        </section>

        <section className="dashboard-control-panel">
          <header className="dashboard-panel-title">
            <span>Управление</span>
            <strong>карта, POS, депозит, клиент</strong>
          </header>
          <div className="dashboard-control-grid">
            {controlCards.map(([label, value, detail, Icon]) => (
              <DashboardControlCard
                key={label}
                label={label}
                value={value}
                detail={detail}
                icon={Icon}
                onActivate={() => triggerFeedback(setFeedback, `Переход ${label}`)}
              />
            ))}
          </div>
        </section>

        <section className="dashboard-pulse-panel">
          <header className="dashboard-panel-title">
            <span>Пульс смены</span>
            <strong>касса, зал, сигналы, брони</strong>
          </header>
          <div className="dashboard-pulse-grid">
            {pulseItems.map((item) => (
              <DashboardPulseCard key={item.label} {...item} />
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function DashboardControlCard({
  label,
  value,
  detail,
  icon: Icon,
  onActivate
}: {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
  onActivate: () => void;
}) {
  return (
    <button type="button" className="dashboard-control-card" onClick={onActivate}>
      <span>
        <Icon size={16} />
        {label}
      </span>
      <strong>{value}</strong>
      <em>{detail}</em>
    </button>
  );
}

function DashboardPulseCard({
  label,
  value,
  detail,
  chartValue,
  chartLabel,
  chartSubLabel,
  tone,
  icon: Icon
}: {
  label: string;
  value: string;
  detail: string;
  chartValue: number;
  chartLabel: ReactNode;
  chartSubLabel: string;
  tone: string;
  icon: LucideIcon;
}) {
  return (
    <article className={`dashboard-pulse-card ${tone}`}>
      <header className="pulse-card-title">
        <Icon size={15} />
        <span>{label}</span>
      </header>
      <div
        className="donut-chart"
        style={{ '--chart-value': `${chartValue}%` } as CSSProperties}
        aria-hidden="true"
      >
        <strong>{chartLabel}</strong>
        <em>{chartSubLabel}</em>
      </div>
    </article>
  );
}

function BookingWorkspace() {
  const [selectedBookingIndex, setSelectedBookingIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const bookings = [
    { time: '15:40', client: 'Aziz P.', seats: '2 ПК', zone: 'Зал C', duration: '90 мин', status: 'Подтверждена', tone: 'confirmed', note: 'прийти за 10 мин до старта' },
    { time: '16:00', client: 'Гость +998', seats: '1 ПК', zone: 'VIP', duration: '60 мин', status: 'Онлайн-заявка', tone: 'online', note: 'нужен звонок для подтверждения' },
    { time: '16:30', client: 'Team CS2', seats: '5 ПК', zone: 'Bootcamp', duration: '120 мин', status: 'Строгая', tone: 'strict', note: 'держать места вместе' },
    { time: '17:10', client: 'Madina S.', seats: '1 ПК', zone: 'Зал B', duration: '45 мин', status: 'Ожидает', tone: 'pending', note: 'нет депозита, уточнить оплату' }
  ];

  const requests = [
    ['15:55', 'Telegram · +992 90 555 11 22', '2 ПК · рядом · 90 мин'],
    ['16:20', 'Сайт · guest-1842', '1 VIP · 60 мин']
  ];
  const selectedBooking = bookings[selectedBookingIndex];

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <span>Брони</span>
          <h1>Брони сегодня · посадка гостей и онлайн-заявки</h1>
        </div>
        <div className="screen-actions">
          <button type="button" className="booking-create-action" onClick={() => triggerFeedback(setFeedback, 'Новая бронь')}><Plus size={14} />Создать</button>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label="Ближайшая" value="15:40" />
        <StateFlag label="Активные" value="5" />
        <StateFlag label="Онлайн" value="2" critical />
        <StateFlag label="Конфликты" value="1" critical />
        <StateFlag label="Слоты" value="8" />
      </section>

      <section className="booking-layout">
        <section className="booking-panel booking-timeline-panel">
          <header className="booking-panel-title">
            <span>Лента броней</span>
            <strong>ближайшие посадки сегодня</strong>
          </header>
          <div className="booking-list">
            {bookings.map((booking) => (
              <button
                key={`${booking.time}-${booking.client}`}
                type="button"
                className={`booking-card ${booking.tone}${booking === selectedBooking ? ' active' : ''}`}
                onClick={() => setSelectedBookingIndex(bookings.indexOf(booking))}
              >
                <span className="booking-time">{booking.time}</span>
                <span className="booking-client">
                  <strong>{booking.client}</strong>
                  <em>{booking.note}</em>
                </span>
                <span className="booking-meta">{booking.seats} · {booking.zone} · {booking.duration}</span>
                <b>{booking.status}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="booking-panel booking-selected-panel">
          <header className="booking-panel-title">
            <span>Выбранная бронь</span>
            <strong>{selectedBooking.client} · {selectedBooking.time}</strong>
          </header>
          <div className={`booking-status-card ${selectedBooking.tone}`}>
            <span>Готовить посадку</span>
            <strong>{selectedBooking.time}</strong>
            <em>{selectedBooking.seats} · {selectedBooking.zone} · {selectedBooking.duration}</em>
          </div>
          <div className="booking-action-grid" aria-label="Действия с бронью">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Открыть карту')}><MonitorCheck size={15} />Открыть карту</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Посадить бронь')}><UserRoundPlus size={15} />Посадить</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Перенести бронь')}><ArrowRightLeft size={15} />Перенести</button>
            <button type="button" className="danger" onClick={() => triggerFeedback(setFeedback, 'Отменить бронь')}><Square size={15} />Отменить</button>
          </div>
          <FeedbackNotice feedback={feedback} />
          <div className="booking-detail-list">
            <div><span>Клиент</span><strong>{selectedBooking.client}</strong></div>
            <div><span>Комментарий</span><strong>{selectedBooking.note}</strong></div>
            <div><span>Подтверждение</span><strong>звонок не нужен</strong></div>
          </div>
        </section>

        <section className="booking-panel booking-requests-panel">
          <header className="booking-panel-title">
            <span>Онлайн-заявки</span>
            <strong>требуют ответа оператора</strong>
          </header>
          <div className="booking-request-list">
            {requests.map(([time, source, detail]) => (
              <article key={`${time}-${source}`} className="booking-request-card">
                <span>{time}</span>
                <strong>{source}</strong>
                <em>{detail}</em>
                <div>
                  <button type="button" onClick={() => triggerFeedback(setFeedback, `Принять ${time}`)}>Принять</button>
                  <button type="button" onClick={() => triggerFeedback(setFeedback, `Уточнить ${time}`)}>Уточнить</button>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="booking-panel booking-create-panel">
          <header className="booking-panel-title">
            <span>Новая бронь</span>
            <strong>быстрый черновик</strong>
          </header>
          <div className="booking-form-grid">
            <label>Клиент<input value="телефон или имя" readOnly /></label>
            <label>Старт<input value="Сегодня · 16:00" readOnly /></label>
            <label>Длительность<input value="60 мин" readOnly /></label>
            <label>ПК<input value="2 · рядом" readOnly /></label>
          </div>
          <button type="button" className="booking-primary-action" onClick={() => triggerFeedback(setFeedback, 'Создать бронь')}>Создать бронь</button>
        </section>
      </section>
    </main>
  );
}

function PosWorkspace({ currencyCode }: { currencyCode: string }) {
  const [activeCategory, setActiveCategory] = useState('Популярное');
  const [productSearch, setProductSearch] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const products = [
    { name: 'Cola 0.5', price: 12, group: 'напитки', note: 'холодильник', category: 'Напитки' },
    { name: 'Вода 0.5', price: 6, group: 'напитки', note: 'рядом с кассой', category: 'Напитки' },
    { name: 'Хот-дог', price: 28, group: 'кухня', note: '7 мин', category: 'Еда' },
    { name: 'Бургер', price: 42, group: 'кухня', note: '12 мин', category: 'Еда' },
    { name: 'Гостевой час', price: 25, group: 'игровое время', note: 'без клиента', category: 'Услуги' },
    { name: 'VIP час', price: 45, group: 'игровое время', note: 'VIP зона', category: 'Услуги' },
    { name: 'Аренда гарнитуры', price: 10, group: 'услуги', note: 'залог не нужен', category: 'Услуги' },
    { name: 'Gamer combo', price: 55, group: 'комбо', note: 'напиток + еда', category: 'Популярное' }
  ];
  const [cartItems, setCartItems] = useState([
    { name: 'Cola 0.5', quantity: 2, price: 12 },
    { name: 'Гостевой час', quantity: 1, price: 25 },
    { name: 'Аренда гарнитуры', quantity: 1, price: 10 }
  ]);
  const receipts = [
    ['15:08', 'PC-06 · Madina S.', `86 ${currencyCode}`, 'карта'],
    ['14:55', 'PC-04 · возврат', `-20 ${currencyCode}`, 'наличные'],
    ['14:42', 'Гость · стойка', `59 ${currencyCode}`, 'наличные']
  ];
  const quickOps: Array<[string, string, LucideIcon]> = [
    ['Пополнить депозит', 'клиент или телефон', CircleDollarSign],
    ['Возврат по чеку', 'поиск последней продажи', ReceiptText],
    ['Новый клиент', 'быстрая регистрация', UserRoundPlus],
    ['Внести наличные', 'кассовое движение', Banknote]
  ];
  const visibleProducts = products.filter((product) => {
    const categoryMatches = activeCategory === 'Популярное' || product.category === activeCategory || product.category === 'Популярное';
    const searchMatches = `${product.name} ${product.group} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
  const cartTotal = cartItems.reduce((sum, item) => sum + item.price * item.quantity, 0);
  const acceptedCash = paymentMethod === 'Наличные' ? Math.ceil(cartTotal / 10) * 10 : cartTotal;
  const change = acceptedCash - cartTotal;
  const addProduct = (product: (typeof products)[number]) => {
    setCartItems((items) => {
      const existing = items.find((item) => item.name === product.name);

      if (existing) {
        return items.map((item) => item.name === product.name ? { ...item, quantity: item.quantity + 1 } : item);
      }

      return [...items, { name: product.name, quantity: 1, price: product.price }];
    });
    triggerFeedback(setFeedback, `${product.name} добавлен`);
  };

  return (
    <main className="workspace-screen pos-screen">
      <section className="screen-head pos-head">
        <div>
          <span>POS</span>
          <h1>POS · продажа и кассовые операции</h1>
        </div>
      </section>

      <section className="state-strip pos-state-strip" aria-label="Сводка POS">
        <StateFlag label="Продажи" value={`2 чека · 145 ${currencyCode}`} />
        <StateFlag label="Возвраты" value={`1 · 20 ${currencyCode}`} critical />
        <StateFlag label="Наличные" value={`3 740 ${currencyCode}`} />
        <StateFlag label="Склад" value="2 позиции низко" critical />
        <StateFlag label="Смена" value="5ч 54м" />
      </section>

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title">
            <span>Каталог</span>
            <strong>быстрый поиск товара или услуги</strong>
          </header>
          <label className="pos-search">
            <Search size={14} />
            <input
              placeholder="Товар, услуга, клиент, чек"
              value={productSearch}
              onChange={(event) => setProductSearch(event.currentTarget.value)}
            />
          </label>
          <div className="pos-category-row" aria-label="Категории POS">
            {['Популярное', 'Еда', 'Напитки', 'Услуги'].map((category) => (
              <button
                key={category}
                type="button"
                className={activeCategory === category ? 'active' : undefined}
                onClick={() => setActiveCategory(category)}
              >
                {category}
              </button>
            ))}
          </div>
          <div className="pos-catalog-grid">
            {visibleProducts.map((product) => (
              <button key={product.name} type="button" className="pos-product-card" onClick={() => addProduct(product)}>
                <strong>{product.name}</strong>
                <span>{product.group}</span>
                <b>{product.price} {currencyCode}</b>
                <em>{product.note}</em>
              </button>
            ))}
          </div>
        </section>

        <section className="pos-panel pos-cart-panel">
          <header className="pos-panel-title">
            <span>Корзина</span>
            <strong>текущая продажа</strong>
          </header>
          <div className="pos-cart-client">
            <UserRoundPlus size={17} />
            <div>
              <span>Клиент</span>
              <strong>Гость · без карты</strong>
            </div>
            <button type="button">Выбрать</button>
          </div>
          <div className="pos-cart-list">
            {cartItems.map((item) => (
              <article key={item.name} className="pos-cart-row interactive-row">
                <div>
                  <strong>{item.name}</strong>
                  <span>{item.quantity} шт.</span>
                </div>
                <b>{item.price * item.quantity} {currencyCode}</b>
              </article>
            ))}
          </div>
          <div className="pos-total-card">
            <span>Итого к оплате</span>
            <strong><AnimatedNumber value={cartTotal} /> {currencyCode}</strong>
            <em>скидок нет · чек будет создан после подтверждения платформы</em>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="pos-panel pos-payment-panel">
          <header className="pos-panel-title">
            <span>Оплата</span>
            <strong>способ и подтверждение</strong>
          </header>
          <div className="pos-payment-methods">
            {['Наличные', 'Карта', 'Депозит'].map((method) => (
              <button
                key={method}
                type="button"
                className={paymentMethod === method ? 'active' : undefined}
                onClick={() => setPaymentMethod(method)}
              >
                {method === 'Наличные' && <Banknote size={15} />}
                {method === 'Карта' && <CircleDollarSign size={15} />}
                {method === 'Депозит' && <ReceiptText size={15} />}
                {method}
              </button>
            ))}
          </div>
          <div className="pos-payment-summary">
            <div><span>Принято</span><strong><AnimatedNumber value={acceptedCash} /> {currencyCode}</strong></div>
            <div><span>Сдача</span><strong><AnimatedNumber value={change} /> {currencyCode}</strong></div>
            <div><span>Смена</span><strong>Открыта</strong></div>
          </div>
          <button type="button" className="pos-primary-action" onClick={() => triggerFeedback(setFeedback, 'Оплата')}>Принять оплату</button>
          <button type="button" className="pos-secondary-action" onClick={() => triggerFeedback(setFeedback, 'Чек отложен')}>Отложить чек</button>
        </section>

        <section className="pos-panel pos-receipts-panel">
          <header className="pos-panel-title">
            <span>Последние чеки</span>
            <strong>быстрый доступ к возврату и повтору</strong>
          </header>
          <div className="pos-receipt-list">
            {receipts.map(([time, source, total, method]) => (
              <article key={`${time}-${source}`} className="pos-receipt-row">
                <span>{time}</span>
                <strong>{source}</strong>
                <em>{method}</em>
                <b>{total}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="pos-panel pos-quick-panel">
          <header className="pos-panel-title">
            <span>Быстрые операции</span>
            <strong>касса без лишних переходов</strong>
          </header>
          <div className="pos-quick-grid">
            {quickOps.map(([label, detail, Icon]) => (
              <button key={label} type="button" className="pos-quick-card" onClick={() => triggerFeedback(setFeedback, label)}>
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function PlayersWorkspace({ currencyCode }: { currencyCode: string }) {
  const [clientSearch, setClientSearch] = useState('');
  const [activeSegment, setActiveSegment] = useState('Все');
  const [selectedClientName, setSelectedClientName] = useState('Madina S.');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const clients = [
    { name: 'Madina S.', status: 'VIP', balance: `460 ${currencyCode}`, debt: `0 ${currencyCode}`, last: 'вчера · Dota 2', tone: 'vip', detail: '+10% скидка · 42 визита' },
    { name: 'Amir K.', status: 'Активен', balance: `120 ${currencyCode}`, debt: `0 ${currencyCode}`, last: 'сейчас · PC-01', tone: 'active', detail: 'пакет до 18:40 · 18 визитов' },
    { name: 'Olim K.', status: 'Долг', balance: `0 ${currencyCode}`, debt: `35 ${currencyCode}`, last: 'сейчас · PC-04', tone: 'debt', detail: 'постоплата близко к лимиту' },
    { name: 'Yusuf A.', status: 'Обычный', balance: `0 ${currencyCode}`, debt: `0 ${currencyCode}`, last: '18 мая · CS2', tone: 'regular', detail: 'нет активного пакета' },
    { name: 'Aziz P.', status: 'Бронь', balance: `80 ${currencyCode}`, debt: `0 ${currencyCode}`, last: 'бронь 15:40', tone: 'booking', detail: '2 ПК · Зал C' }
  ];
  const visibleClients = clients.filter((client) => {
    const segmentMatches = activeSegment === 'Все'
      || (activeSegment === 'VIP' && client.status === 'VIP')
      || (activeSegment === 'Есть долг' && parseMoney(client.debt) > 0)
      || (activeSegment === 'Новые' && client.name === 'Yusuf A.')
      || (activeSegment === 'Спящие' && client.name === 'Yusuf A.');
    const searchMatches = `${client.name} ${client.status} ${client.detail} ${client.last}`
      .toLowerCase()
      .includes(clientSearch.trim().toLowerCase());
    return segmentMatches && searchMatches;
  });
  const selectedClient = clients.find((client) => client.name === selectedClientName) ?? visibleClients[0] ?? clients[0];
  const history = [
    ['Вчера 22:10', 'Пополнение депозита', `200 ${currencyCode}`],
    ['Вчера 20:42', 'VIP час · PC-06', `45 ${currencyCode}`],
    ['15 мая', 'Возврат по чеку', `-20 ${currencyCode}`]
  ];
  const quickOps: Array<[string, string, LucideIcon]> = [
    ['Пополнить депозит', 'наличные или карта', CircleDollarSign],
    ['Списать долг', 'после оплаты', ReceiptText],
    ['Создать бронь', 'сразу из карточки', CalendarClock],
    ['Новая карта', 'быстрая регистрация', UserRoundPlus]
  ];

  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>Клиенты</span>
          <h1>Клиенты · поиск, депозит и долги</h1>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label="Сводка клиентов">
        <StateFlag label="Клиенты" value="12 480" />
        <StateFlag label="Онлайн" value="9" />
        <StateFlag label="Депозиты" value={`84 210 ${currencyCode}`} />
        <StateFlag label="Долги" value={`1 240 ${currencyCode}`} critical />
        <StateFlag label="VIP" value="314" />
      </section>

      <section className="clients-layout">
        <section className="clients-panel clients-list-panel">
          <header className="clients-panel-title">
            <span>Список клиентов</span>
            <strong>поиск по имени, телефону или карте</strong>
          </header>
          <label className="clients-search">
            <Search size={14} />
            <input
              placeholder="Игрок, телефон, карта"
              value={clientSearch}
              onChange={(event) => setClientSearch(event.currentTarget.value)}
            />
          </label>
          <div className="clients-list">
            {visibleClients.map((client) => (
              <button
                key={client.name}
                type="button"
                className={`client-row ${client.tone}${client.name === selectedClient.name ? ' selected' : ''}`}
                onClick={() => setSelectedClientName(client.name)}
              >
                <span>{client.status}</span>
                <div>
                  <strong>{client.name}</strong>
                  <em>{client.detail}</em>
                </div>
                <b>{client.balance}</b>
                <small>{client.last}</small>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-profile-panel">
          <header className="clients-panel-title">
            <span>Карточка клиента</span>
            <strong>выбранный игрок</strong>
          </header>
          <div className="client-profile-card">
            <div className="client-avatar">MS</div>
            <div>
              <span>{selectedClient.status}</span>
              <strong>{selectedClient.name}</strong>
              <em>+992 90 555 22 11 · карта 0482</em>
            </div>
          </div>
          <div className="client-metrics-grid">
            <div><span>Депозит</span><strong>{selectedClient.balance}</strong></div>
            <div><span>Долг</span><strong>{selectedClient.debt}</strong></div>
            <div><span>Скидка</span><strong>10%</strong></div>
            <div><span>Визиты</span><strong>42</strong></div>
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>Операции</span>
            <strong>деньги и быстрые действия</strong>
          </header>
          <div className="clients-action-grid">
            {quickOps.map(([label, detail, Icon]) => (
              <button key={label} type="button" className="clients-action-card" onClick={() => triggerFeedback(setFeedback, `${label}: ${selectedClient.name}`)}>
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="clients-panel clients-segments-panel">
          <header className="clients-panel-title">
            <span>Сегменты</span>
            <strong>контекст для оператора</strong>
          </header>
          <div className="clients-segment-grid">
            {[
              ['Все', '12 480 клиентов · вся база'],
              ['VIP', '314 клиентов · 10% скидка'],
              ['Есть долг', '18 клиентов · проверить до закрытия'],
              ['Спящие', '924 клиента · 30+ дней без визита'],
              ['Новые', '36 регистраций за неделю']
            ].map(([label, detail]) => (
              <button
                key={label}
                type="button"
                className={activeSegment === label ? 'active' : undefined}
                onClick={() => setActiveSegment(label)}
              >
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-history-panel">
          <header className="clients-panel-title">
            <span>История клиента</span>
            <strong>последние операции</strong>
          </header>
          <div className="clients-history-list">
            {history.map(([time, event, total]) => (
              <article key={`${time}-${event}`} className="client-history-row">
                <span>{time}</span>
                <strong>{event}</strong>
                <b>{total}</b>
              </article>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function PaymentsWorkspace({ currencyCode }: { currencyCode: string }) {
  const [paymentSearch, setPaymentSearch] = useState('');
  const [selectedOperationKey, setSelectedOperationKey] = useState('15:08-POS продажа-PC-06 · Madina S.');
  const [selectedMethod, setSelectedMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const operations = [
    ['15:08', 'POS продажа', 'PC-06 · Madina S.', 'карта', `86 ${currencyCode}`, 'sale'],
    ['14:55', 'Возврат', 'Yusuf A.', 'наличные', `-20 ${currencyCode}`, 'refund'],
    ['14:41', 'Пополнение', 'Madina S.', 'карта', `200 ${currencyCode}`, 'deposit'],
    ['14:30', 'POS продажа', 'Гость · стойка', 'наличные', `59 ${currencyCode}`, 'sale'],
    ['14:22', 'Игровое время', 'Amir K.', 'депозит', `45 ${currencyCode}`, 'session']
  ];
  const methods = [
    ['Наличные', `2 310 ${currencyCode}`, '48%', '3 операции'],
    ['Карта', `1 940 ${currencyCode}`, '40%', '4 операции'],
    ['Депозит', `570 ${currencyCode}`, '12%', '2 операции'],
    ['Возвраты', `20 ${currencyCode}`, '1 чек', 'проверить']
  ];
  const cashMoves = [
    ['09:02', 'Открытие смены', `1 000 ${currencyCode}`],
    ['13:20', 'Внесение', `300 ${currencyCode}`],
    ['14:55', 'Возврат', `-20 ${currencyCode}`]
  ];
  const visibleOperations = operations.filter(([time, type, client, method, total]) => (
    `${time} ${type} ${client} ${method} ${total}`.toLowerCase().includes(paymentSearch.trim().toLowerCase())
  ));
  const selectedOperation = operations.find(([time, type, client]) => `${time}-${type}-${client}` === selectedOperationKey) ?? operations[0];

  return (
    <main className="workspace-screen payments-screen">
      <section className="screen-head payments-head">
        <div>
          <span>Платежи</span>
          <h1>Платежи · касса смены и сверка</h1>
        </div>
      </section>

      <section className="state-strip payments-state-strip" aria-label="Сводка платежей">
        <StateFlag label="Выручка" value={`4 820 ${currencyCode}`} />
        <StateFlag label="Наличные" value={`2 310 ${currencyCode}`} />
        <StateFlag label="Карта" value={`1 940 ${currencyCode}`} />
        <StateFlag label="Депозиты" value={`570 ${currencyCode}`} />
        <StateFlag label="К сверке" value={`20 ${currencyCode}`} critical />
      </section>

      <section className="payments-layout">
        <section className="payments-panel payments-ledger-panel">
          <header className="payments-panel-title">
            <span>Операции смены</span>
            <strong>продажи, пополнения и возвраты</strong>
          </header>
          <label className="payments-search">
            <Search size={14} />
            <input
              placeholder="Клиент, чек, ПК, сумма"
              value={paymentSearch}
              onChange={(event) => setPaymentSearch(event.currentTarget.value)}
            />
          </label>
          <div className="payments-ledger-list">
            {visibleOperations.map(([time, type, client, method, total, tone]) => (
              <button
                key={`${time}-${type}-${client}`}
                type="button"
                className={`payment-operation-row ${tone}${`${time}-${type}-${client}` === selectedOperationKey ? ' active' : ''}`}
                onClick={() => setSelectedOperationKey(`${time}-${type}-${client}`)}
              >
                <span>{time}</span>
                <div>
                  <strong>{type}</strong>
                  <em>{client}</em>
                </div>
                <small>{method}</small>
                <b>{total}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-summary-panel">
          <header className="payments-panel-title">
            <span>Итоги смены</span>
            <strong>оперативная выручка</strong>
          </header>
          <div className="payments-total-card">
            <span>Всего за смену · выбрано {selectedOperation[0]}</span>
            <strong>4 820 {currencyCode}</strong>
            <em>{selectedOperation[1]} · {selectedOperation[2]} · {selectedOperation[4]}</em>
          </div>
          <div className="payments-metric-grid">
            <div><span>Чеков</span><strong>9</strong></div>
            <div><span>Средний чек</span><strong>536 {currencyCode}</strong></div>
            <div><span>Возвраты</span><strong>1</strong></div>
            <div><span>Долги</span><strong>3</strong></div>
          </div>
        </section>

        <section className="payments-panel payments-reconcile-panel">
          <header className="payments-panel-title">
            <span>Сверка кассы</span>
            <strong>перед закрытием</strong>
          </header>
          <div className="payments-reconcile-list">
            <div><span>Ожидается</span><strong>3 740 {currencyCode}</strong></div>
            <div><span>Посчитано</span><strong>3 720 {currencyCode}</strong></div>
            <div className="attention"><span>Расхождение</span><strong>20 {currencyCode}</strong></div>
          </div>
          <button type="button" className="payments-primary-action" onClick={() => triggerFeedback(setFeedback, 'Подготовить закрытие')}>Подготовить закрытие</button>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="payments-panel payments-methods-panel">
          <header className="payments-panel-title">
            <span>Методы оплаты</span>
            <strong>структура выручки</strong>
          </header>
          <div className="payments-method-grid">
            {methods.map(([label, total, share, detail]) => (
              <button
                key={label}
                type="button"
                className={`payment-method-card${selectedMethod === label ? ' active' : ''}`}
                onClick={() => setSelectedMethod(label)}
              >
                <strong>{label}</strong>
                <b>{total}</b>
                <span>{share} · {detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-cash-panel">
          <header className="payments-panel-title">
            <span>Движение наличных</span>
            <strong>кассовые события</strong>
          </header>
          <div className="payments-cash-list">
            {cashMoves.map(([time, event, total]) => (
              <article key={`${time}-${event}`} className="payment-cash-row">
                <span>{time}</span>
                <strong>{event}</strong>
                <b>{total}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-export-panel">
          <header className="payments-panel-title">
            <span>Отчёты</span>
            <strong>экспорт и журнал</strong>
          </header>
          <div className="payments-export-grid">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Журнал смены')}><ReceiptText size={16} />Журнал смены</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Кассовый отчёт')}><Banknote size={16} />Кассовый отчёт</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Экспорт CSV')}><ArrowRightLeft size={16} />Экспорт CSV</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Расхождения')}><ShieldAlert size={16} />Расхождения</button>
          </div>
        </section>
      </section>
    </main>
  );
}

function LogsWorkspace({ currencyCode }: { currencyCode: string }) {
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState('Все события');
  const [selectedEventKey, setSelectedEventKey] = useState('15:04-PC-23 heartbeat missed');
  const [selectedSource, setSelectedSource] = useState('Agent');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const events = [
    ['15:09', 'Настройки просмотрены', 'Настройки · technician', 'audit', 'audit'],
    ['15:06', 'Пополнение депозита', `Madina S. · 200 ${currencyCode}`, 'cashier', 'money'],
    ['15:04', 'PC-23 heartbeat missed', 'Agent · нет связи 2 мин', 'warning', 'device'],
    ['15:01', 'PC-01 сессия продлена', 'operator · +15 мин', 'session', 'session'],
    ['14:55', 'Возврат по чеку', `Yusuf A. · -20 ${currencyCode}`, 'refund', 'money']
  ];
  const auditRows = [
    ['15:09', 'technician', 'Settings read', 'разрешено'],
    ['15:06', 'cashier', 'Deposit replenished', 'подтверждено'],
    ['15:01', 'operator', 'Session extend', 'платформа OK']
  ];
  const sourceCards: Array<[string, string, LucideIcon]> = [
    ['Agent', '23 онлайн · 1 офлайн', MonitorCheck],
    ['POS', '9 чеков · 1 возврат', ReceiptText],
    ['Operator', '14 действий смены', UserRoundPlus],
    ['Platform', '3 предупреждения', ShieldAlert]
  ];
  const visibleEvents = events.filter(([time, title, detail, source, tone]) => {
    const filterMatches = activeLogFilter === 'Все события'
      || (activeLogFilter === 'Только ошибки' && tone === 'warning')
      || (activeLogFilter === 'ПК и Agent' && (source === 'warning' || detail.includes('Agent')))
      || (activeLogFilter === 'Касса и POS' && tone === 'money')
      || (activeLogFilter === 'Оператор' && detail.includes('operator'))
      || (activeLogFilter === 'Системные' && source === 'audit');
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && searchMatches;
  });
  const selectedEvent = events.find(([time, title]) => `${time}-${title}` === selectedEventKey) ?? visibleEvents[0] ?? events[0];

  return (
    <main className="workspace-screen logs-screen">
      <section className="screen-head logs-head">
        <div>
          <span>Логи</span>
          <h1>Логи · аудит и события смены</h1>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label="Сводка логов">
        <StateFlag label="События" value="128" />
        <StateFlag label="Ошибки" value="3" critical />
        <StateFlag label="Команды" value="12" />
        <StateFlag label="Касса" value="9" />
        <StateFlag label="Аудит" value="6" />
      </section>

      <section className="logs-layout">
        <section className="logs-panel logs-events-panel">
          <header className="logs-panel-title">
            <span>Журнал событий</span>
            <strong>поиск по ПК, клиенту, оператору или событию</strong>
          </header>
          <label className="logs-search">
            <Search size={14} />
            <input
              placeholder="ПК, клиент, оператор, событие"
              value={eventSearch}
              onChange={(event) => setEventSearch(event.currentTarget.value)}
            />
          </label>
          <div className="logs-event-list">
            {visibleEvents.map(([time, title, detail, source, tone]) => (
              <button
                key={`${time}-${title}`}
                type="button"
                className={`log-event-row ${tone}${`${time}-${title}` === selectedEventKey ? ' active' : ''}`}
                onClick={() => setSelectedEventKey(`${time}-${title}`)}
              >
                <span>{time}</span>
                <div>
                  <strong>{title}</strong>
                  <em>{detail}</em>
                </div>
                <b>{source}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-detail-panel">
          <header className="logs-panel-title">
            <span>Детали события</span>
            <strong>выбранная запись</strong>
          </header>
          <div className={`log-detail-card ${selectedEvent[4]}`}>
            <span>{selectedEvent[0]} · {selectedEvent[3]}</span>
            <strong>{selectedEvent[1]}</strong>
            <em>{selectedEvent[2]}</em>
          </div>
          <div className="log-detail-list">
            <div><span>Источник</span><strong>{selectedEvent[3]}</strong></div>
            <div><span>Объект</span><strong>{selectedEvent[1].includes('PC-') ? selectedEvent[1].split(' ')[0] : 'смена #24'}</strong></div>
            <div><span>Оператор</span><strong>system</strong></div>
            <div><span>Correlation</span><strong>evt-9f42</strong></div>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="logs-panel logs-filter-panel">
          <header className="logs-panel-title">
            <span>Фильтры</span>
            <strong>сузить расследование</strong>
          </header>
          <div className="logs-filter-grid">
            {['Все события', 'Только ошибки', 'ПК и Agent', 'Касса и POS', 'Оператор', 'Системные'].map((filter) => (
              <button
                key={filter}
                type="button"
                className={activeLogFilter === filter ? 'active' : undefined}
                onClick={() => setActiveLogFilter(filter)}
              >
                {filter}
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-audit-panel">
          <header className="logs-panel-title">
            <span>Аудит смены</span>
            <strong>действия персонала</strong>
          </header>
          <div className="logs-audit-list">
            {auditRows.map(([time, actor, action, result]) => (
              <article key={`${time}-${actor}-${action}`} className="log-audit-row">
                <span>{time}</span>
                <strong>{actor}</strong>
                <em>{action}</em>
                <b>{result}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-sources-panel">
          <header className="logs-panel-title">
            <span>Источники</span>
            <strong>откуда пришли события</strong>
          </header>
          <div className="logs-source-grid">
            {sourceCards.map(([label, detail, Icon]) => (
              <button
                key={label}
                type="button"
                className={`log-source-card${selectedSource === label ? ' active' : ''}`}
                onClick={() => setSelectedSource(label)}
              >
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-export-panel">
          <header className="logs-panel-title">
            <span>Экспорт</span>
            <strong>для проверки и поддержки</strong>
          </header>
          <div className="logs-export-grid">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Журнал смены')}><ReceiptText size={16} />Журнал смены</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Ошибки')}><AlertTriangle size={16} />Ошибки</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'CSV')}><ArrowRightLeft size={16} />CSV</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Audit trail')}><ShieldAlert size={16} />Audit trail</button>
          </div>
        </section>
      </section>
    </main>
  );
}

function SettingsWorkspace() {
  const [selectedSection, setSelectedSection] = useState('Профиль клуба');
  const [clubName, setClubName] = useState('AFK4 Dushanbe');
  const [city, setCity] = useState('Dushanbe');
  const [settingsDirty, setSettingsDirty] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const sections = [
    ['Профиль клуба', 'название, город, валюта'],
    ['Залы и ПК', 'зоны, рабочие места, статусы'],
    ['Тарифы', 'пакеты, постоплата, VIP'],
    ['Персонал', 'операторы, роли, доступы'],
    ['POS и склад', 'товары, остатки, чеки'],
    ['Интеграции', 'платежи, уведомления, экспорт']
  ];
  const rooms = [
    ['Зал A', '10 ПК', 'основной зал'],
    ['Зал B', '8 ПК', 'тихий зал'],
    ['VIP', '2 ПК', 'повышенный тариф'],
    ['Bootcamp', '4 ПК', 'командные места']
  ];
  const tariffs = [
    ['Стандарт', '25 TJS / час', 'для гостей и обычных клиентов'],
    ['VIP', '45 TJS / час', 'для VIP-зоны'],
    ['Ночь', '120 TJS / пакет', 'после 23:00'],
    ['Постоплата', 'лимит 100 TJS', 'только для доверенных клиентов']
  ];
  const readiness = [
    ['Профиль клуба', 'заполнен'],
    ['Залы и ПК', '24 рабочих места'],
    ['Персонал', '4 роли'],
    ['Касса', 'TJS · смена открыта'],
    ['Устройства', '23 из 24 онлайн']
  ];
  const actions: Array<[string, string, LucideIcon]> = [
    ['Добавить ПК', 'новое рабочее место на карте', MonitorCheck],
    ['Создать тариф', 'пакет или почасовая цена', CircleDollarSign],
    ['Пригласить сотрудника', 'оператор или техник', UserRoundPlus],
    ['Проверить устройства', 'Agent и Shell', Wifi]
  ];
  const selectedSectionDetail = sections.find(([name]) => name === selectedSection)?.[1] ?? '';
  const markDirty = () => setSettingsDirty(true);
  const saveSettings = () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, 'Проверить обязательные поля', 'failed');
      return;
    }

    setSettingsDirty(false);
    triggerFeedback(setFeedback, 'Настройки сохранены');
  };

  const renderSettingsContent = () => {
    if (selectedSection === 'Залы и ПК') {
      return (
        <>
          <div className="settings-section-title">
            <span>Залы и рабочие места</span>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Добавить зал')}>Добавить зал</button>
          </div>
          <div className="settings-room-grid">
            {rooms.map(([name, count, detail]) => (
              <button key={name} type="button" className="settings-room-card" onClick={() => triggerFeedback(setFeedback, `Открыть ${name}`)}>
                <strong>{name}</strong>
                <b>{count}</b>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </>
      );
    }

    if (selectedSection === 'Тарифы') {
      return (
        <>
          <div className="settings-section-title">
            <span>Тарифы</span>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Создать тариф')}>Создать тариф</button>
          </div>
          <div className="settings-tariff-list">
            {tariffs.map(([name, price, detail]) => (
              <button key={name} type="button" className="settings-tariff-row" onClick={() => triggerFeedback(setFeedback, `Открыть тариф ${name}`)}>
                <strong>{name}</strong>
                <b>{price}</b>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </>
      );
    }

    if (selectedSection === 'Персонал') {
      return (
        <div className="settings-config-grid">
          {['Owner · полный доступ', 'Manager · смены и отчёты', 'Operator · касса и карта', 'Technician · устройства'].map((role) => (
            <button key={role} type="button" onClick={() => triggerFeedback(setFeedback, role)}>
              <strong>{role.split(' · ')[0]}</strong>
              <span>{role.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'POS и склад') {
      return (
        <div className="settings-config-grid">
          {['Напитки · 18 позиций', 'Кухня · 7 позиций', 'Услуги · 4 позиции', 'Низкие остатки · 2 товара'].map((item) => (
            <button key={item} type="button" onClick={() => triggerFeedback(setFeedback, item)}>
              <strong>{item.split(' · ')[0]}</strong>
              <span>{item.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'Интеграции') {
      return (
        <div className="settings-config-grid">
          {['Платежи · manual provider', 'Уведомления · выключены', 'Экспорт · CSV включён', 'API · staging'].map((item) => (
            <button key={item} type="button" onClick={() => triggerFeedback(setFeedback, item)}>
              <strong>{item.split(' · ')[0]}</strong>
              <span>{item.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>Название клуба<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); markDirty(); }} /></label>
          <label>Город<input value={city} onChange={(event) => { setCity(event.currentTarget.value); markDirty(); }} /></label>
          <label>Валюта<input value="TJS" readOnly /></label>
          <label>Часовой пояс<input value="Asia/Dushanbe" readOnly /></label>
        </div>
        <div className="settings-save-row">
          <span>{settingsDirty ? 'есть несохранённые изменения' : 'изменений нет'}</span>
          <button type="button" onClick={saveSettings}>Сохранить</button>
        </div>
      </>
    );
  };

  return (
    <main className="workspace-screen settings-screen">
      <section className="screen-head settings-head">
        <div>
          <span>Настройки</span>
          <h1>Настройки · клуб и правила работы</h1>
        </div>
      </section>

      <section className="settings-layout">
        <aside className="settings-nav-panel">
          <span>Разделы</span>
          {sections.map(([name, detail]) => (
            <button
              key={name}
              type="button"
              className={selectedSection === name ? 'active' : undefined}
              onClick={() => setSelectedSection(name)}
            >
              <strong>{name}</strong>
              <em>{detail}</em>
            </button>
          ))}
        </aside>

        <section className="settings-main-panel">
          <header className="settings-panel-title">
            <span>{selectedSection}</span>
            <strong>{selectedSectionDetail}</strong>
          </header>
          {renderSettingsContent()}
          <FeedbackNotice feedback={feedback} />
        </section>

        <aside className="settings-side-panel">
          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>Готовность клуба</span>
              <strong>что важно перед запуском</strong>
            </header>
            <div className="settings-readiness-list">
              {readiness.map(([name, detail]) => (
                <div key={name}>
                  <span>{name}</span>
                  <strong>{detail}</strong>
                </div>
              ))}
            </div>
          </section>

          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>Быстрые настройки</span>
              <strong>частые действия администратора</strong>
            </header>
            <div className="settings-action-grid">
              {actions.map(([label, detail, Icon]) => (
                <button key={label} type="button" className="settings-action-card" onClick={() => triggerFeedback(setFeedback, label)}>
                  <Icon size={17} />
                  <strong>{label}</strong>
                  <span>{detail}</span>
                </button>
              ))}
            </div>
          </section>

        </aside>
      </section>
    </main>
  );
}

function MapSidePanel({ seat, currencyCode }: { seat: SeatSummary; currencyCode: string }) {
  const status = mapSeatStatus(seat);
  const activeBilling = billingLabel(seat.billing);
  const billingModes = ['Гость', 'Депозит', 'Пакет', 'Постоплата'];
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>{seat.zone}</span>
          <h2>{seat.name}</h2>
        </div>
        <span className={`state-chip state-${seat.tone}`}>{toneLabels[seat.tone]}</span>
      </header>

      <section className={`context-status-row state-${seat.tone}`}>
        <span>{status.label}</span>
        <strong>{status.value}</strong>
      </section>

      <section className="action-grid context-actions" aria-label="Быстрые действия">
        <button type="button" onClick={() => triggerFeedback(setFeedback, '+15 мин')}><Plus size={15} />15 мин</button>
        <button type="button" onClick={() => triggerFeedback(setFeedback, '+30 мин')}><TimerReset size={15} />30 мин</button>
        <button type="button" onClick={() => triggerFeedback(setFeedback, 'Перенос')}><ArrowRightLeft size={15} />Перенос</button>
        <button type="button" className="danger" onClick={() => triggerFeedback(setFeedback, 'Стоп')}><Square size={15} />Стоп</button>
      </section>
      <FeedbackNotice feedback={feedback} />

      <section className="context-section">
        <div className="session-timer">
          <Clock3 size={17} />
          <div>
            <span>Активная сессия</span>
            <strong>{seat.remaining}</strong>
          </div>
        </div>
        <div className="detail-row">
          <span>Игрок</span>
          <strong>{seat.player}</strong>
        </div>
        <div className="detail-row">
          <span>Биллинг</span>
          <strong>{activeBilling} · {currencyCode}</strong>
        </div>
      </section>

      <section className="context-section">
        <div className="detail-row">
          <span>Устройство</span>
          <strong>{deviceStatusLabel(seat.device)}</strong>
        </div>
        <div className="detail-row">
          <span>Команда</span>
          <strong>{commandLabel(seat.command)}</strong>
        </div>
        <div className="detail-row">
          <span>Подтверждение</span>
          <strong>{feedback.state === 'idle' ? 'Ждём платформу' : feedbackText(feedback)}</strong>
        </div>
      </section>

      <section className="billing-mode" aria-label="Режим биллинга">
        {billingModes.map((mode) => (
          <button key={mode} type="button" className={mode === activeBilling ? 'active' : undefined}>
            {mode}
          </button>
        ))}
      </section>
    </aside>
  );
}

function SummarySidePanel({ workspace, currencyCode }: { workspace: WorkspaceId; currencyCode: string }) {
  const title = {
    map: 'PC-01',
    dashboard: 'Смена #24',
    booking: 'Бронь 16:00',
    pos: 'Корзина',
    players: 'Amir K.',
    payments: 'Платеж 14:30',
    logs: 'Log event',
    settings: 'Настройки'
  }[workspace];

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>Details</span>
          <h2>{title}</h2>
        </div>
        <span className="state-chip state-active">Active</span>
      </header>
      <section className="context-section">
        <div className="detail-row"><span>Revenue</span><strong>4 820 {currencyCode}</strong></div>
        <div className="detail-row"><span>Pending</span><strong>2 actions</strong></div>
        <div className="detail-row"><span>Source</span><strong>SmartShell-like fixture</strong></div>
      </section>
      <button type="button" className="primary-wide">Open action</button>
    </aside>
  );
}

export function App() {
  const config = getOperatorConfig();
  const [workspace, setWorkspace] = useState<WorkspaceId>('map');
  const [selectedSeatId, setSelectedSeatId] = useState(seats[0].id);
  const selectedSeat = seats.find((seat) => seat.id === selectedSeatId) ?? seats[0];

  return (
    <div className="operator-shell">
      <header className="top-command" onMouseDown={handleWindowDragStart}>
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Operator</span>
        </div>
        <label className="command-search">
          <Search size={16} />
          <input placeholder="Игрок, ПК, команда" aria-label="Поиск" />
        </label>
        <div className="top-status">
          <span><Wifi size={14} />Realtime connected</span>
          <span>Смена #24 · открыта</span>
          <span>Dushanbe · {config.shellMode}</span>
        </div>
        <div className="window-controls" aria-label="Окно">
          <button type="button" title="Свернуть" aria-label="Свернуть" onClick={() => postHostWindowCommand('minimize')}>
            <Minus size={15} />
          </button>
          <button type="button" title="Развернуть" aria-label="Развернуть" onClick={() => postHostWindowCommand('maximize')}>
            <Maximize2 size={13} />
          </button>
          <button type="button" title="Закрыть" aria-label="Закрыть" onClick={() => postHostWindowCommand('close')}>
            <X size={15} />
          </button>
        </div>
      </header>

      <nav className="workspace-rail" aria-label="Рабочие места">
        {navItems.map((item, index) => {
          const Icon = item.icon;
          const id = workspaceIds[index];
          return (
            <button
              key={item.label}
              type="button"
              className={workspace === id ? 'active' : ''}
              title={item.label}
              onClick={() => setWorkspace(id)}
            >
              <Icon size={22} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>

      {workspace === 'map' && (
        <MapWorkspace
          currencyCode={config.currencyCode}
          selectedSeatId={selectedSeat.id}
          onSelectSeat={setSelectedSeatId}
        />
      )}
      {workspace === 'dashboard' && <DashboardWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'booking' && <BookingWorkspace />}
      {workspace === 'pos' && <PosWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'players' && <PlayersWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'payments' && <PaymentsWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'logs' && <LogsWorkspace currencyCode={config.currencyCode} />}
      {workspace === 'settings' && <SettingsWorkspace />}

      {workspace === 'map' && <MapSidePanel seat={selectedSeat} currencyCode={config.currencyCode} />}
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings'
        && <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}

      <footer className="signals-strip">
        {signals.map((signal) => {
          const Icon = signal.icon;
          return (
            <span key={signal.label}>
              <Icon size={14} />
              {signal.label}
            </span>
          );
        })}
        <span><LockKeyhole size={14} />Критичные действия ждут подтверждения платформы</span>
      </footer>
    </div>
  );
}
