import { useEffect, useState, type CSSProperties, type ReactNode } from 'react';
import { AlertTriangle, Banknote, CalendarClock, CircleDollarSign, MonitorCheck, ReceiptText, ShieldAlert, UserRoundPlus, Wrench } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { minorToMajor } from '@afk4/money';
import { projectOperatorError } from './apiErrors';
import { useDeferredFlag } from './useDeferredFlag';
import type { DashboardPeriod, Feedback, LoadStatus, OperatorBackendContext, WorkspaceId } from './operatorTypes';
import {
  addDays,
  countPeriodDays,
  createAuthenticatedOperatorClients,
  dashboardFocusTextLabel,
  dashboardRangeQuery,
  downloadTextFile,
  emptyDashboardSummary,
  emptyFeedback,
  formatCompactNumber,
  formatMinorUnits,
  readArray,
  readMoney,
  readNumber,
  readRecord,
  readString,
  requireBackend,
  toDateInputValue,
} from './operatorHelpers';
import { Skeleton } from './operatorPrimitives';
import { useFeedbackToasts } from './useFeedbackToasts';
import type { OperatorDashboardSummaryDto } from './operatorApiClients';

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

export function DashboardWorkspace({
  currencyCode,
  backend,
  onNavigate,
  onOpenSeat
}: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}) {
  const { t } = useI18n();
  const today = new Date();
  const todayInput = toDateInputValue(today);
  const weekStartInput = toDateInputValue(addDays(today, -6));
  const monthStartInput = toDateInputValue(addDays(today, -29));
  const [period, setPeriod] = useState<DashboardPeriod>('today');
  const [customRange, setCustomRange] = useState({ from: weekStartInput, to: todayInput });
  const [selectedFocusIndex, setSelectedFocusIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [dashboardSummary, setDashboardSummary] = useState<OperatorDashboardSummaryDto | null>(null);
  const [dashboardLoadStatus, setDashboardLoadStatus] = useState<LoadStatus>('loading');
  const [dashboardLoadError, setDashboardLoadError] = useState<string | null>(null);
  const showDashboardSkeleton = useDeferredFlag(dashboardLoadStatus === 'loading' && dashboardSummary === null);

  const presetRanges = {
    today: { from: todayInput, to: todayInput, label: t('op.dashboard.range.today'), metricLabel: t('op.dashboard.metric.today') },
    week: { from: weekStartInput, to: todayInput, label: t('op.dashboard.range.week'), metricLabel: t('op.dashboard.metric.week') },
    month: { from: monthStartInput, to: todayInput, label: t('op.dashboard.range.month'), metricLabel: t('op.dashboard.metric.month') }
  };

  const activeRange = period === 'custom'
    ? { ...customRange, label: t('op.dashboard.range.custom'), metricLabel: t('op.dashboard.metric.custom') }
    : presetRanges[period];
  const activeDays = countPeriodDays(activeRange.from, activeRange.to);
  const activePeriodLabel = period === 'custom' ? t('op.dashboard.days', { count: activeDays }) : activeRange.metricLabel;
  const periodDaysShort = t('op.dashboard.days', { count: activeDays });
  const exportLabel = `${activeRange.from} - ${activeRange.to}`;
  const updateCustomRange = (field: 'from' | 'to', value: string) => {
    setCustomRange((range) => ({ ...range, [field]: value }));
    setPeriod('custom');
  };

  useEffect(() => {
    let disposed = false;

    if (backend === null) {
      setDashboardSummary(null);
      setDashboardLoadStatus('failed');
      setDashboardLoadError(t('op.dashboard.noBranch'));
      return undefined;
    }

    setDashboardLoadStatus('loading');
    setDashboardLoadError(null);

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.dashboard.getSummary(backend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to))
      .then((summary) => {
        if (disposed) {
          return;
        }

        setDashboardSummary(summary);
        setDashboardLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setDashboardSummary(null);
        setDashboardLoadStatus('failed');
        setDashboardLoadError(projectOperatorError(error, t).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, activeRange.from, activeRange.to]);

  const summary = dashboardSummary ?? emptyDashboardSummary(currencyCode, activeRange.from, activeRange.to);
  const revenue = readRecord(summary, 'revenue');
  const utilization = readRecord(summary, 'utilization');
  const alertPressure = readRecord(summary, 'alertPressure');
  const reservations = readRecord(summary, 'reservations');
  const shift = readRecord(summary, 'shift');
  const totalRevenue = readMoney(revenue, 'totalRevenue') ?? { currencyCode, minorUnits: 0 };
  const expectedCash = readMoney(shift, 'expectedCash') ?? { currencyCode, minorUnits: 0 };
  const cashTargetMinorUnits = Math.max(expectedCash.minorUnits, totalRevenue.minorUnits);
  const cashPercent = cashTargetMinorUnits > 0
    ? Math.min(100, Math.round((totalRevenue.minorUnits / cashTargetMinorUnits) * 100))
    : 0;
  const attentionCount = readNumber(alertPressure, 'totalAlerts', 0);
  const bookingUsed = readNumber(reservations, 'activeReservations', 0);
  const bookingSlots = readNumber(reservations, 'availableSlots', 0);
  const posChecks = readNumber(revenue, 'posCheckCount', 0);
  const newClients = readNumber(revenue, 'newPlayerCount', 0);
  const activePcs = readNumber(utilization, 'activeSessions', 0);
  const totalPcs = Math.max(1, readNumber(utilization, 'totalSeats', 0));
  const focusQueue = readArray<Record<string, unknown>>(summary, 'focusQueue');
  const dashboardStatusText = dashboardLoadStatus === 'backend'
    ? t('op.dashboard.status.backend')
    : dashboardLoadStatus === 'loading'
      ? t('op.dashboard.status.loading')
      : t('op.dashboard.status.error');
  const focusItems = focusQueue.length > 0
    ? focusQueue.map((item) => [
      readString(item, 'tone', 'warning'),
      readString(item, 'target', '-'),
      dashboardFocusTextLabel(readString(item, 'title', t('op.dashboard.signalPlatform')), t),
      dashboardFocusTextLabel(readString(item, 'detail', t('op.dashboard.signalDetail')), t),
      readString(item, 'seatId')
    ] as const)
    : [[
      'ready',
      '-',
      dashboardLoadStatus === 'failed' ? t('op.dashboard.noData') : t('op.dashboard.noUrgent'),
      dashboardLoadStatus === 'failed' ? dashboardLoadError ?? t('op.dashboard.retryLoad') : t('op.dashboard.noUrgentTasks'),
      ''
    ] as const];
  const selectedFocus = focusItems[selectedFocusIndex] ?? focusItems[0];

  const openSelectedFocusSeat = (label: string) => {
    if (selectedFocus[4]) {
      onOpenSeat(selectedFocus[4]);
      return;
    }

    setFeedback({
      label,
      state: 'failed',
      detail: selectedFocus[3]
    });
  };

  const exportDashboard = async () => {
    setFeedback({ label: t('op.dashboard.export'), state: 'pending' });

    try {
      const nextBackend = requireBackend(backend, t);
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [, salesCsv] = await Promise.all([
        clients.dashboard.getSummary(nextBackend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to)),
        clients.shifts.exportSalesReportCsv(nextBackend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to))
      ]);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      downloadTextFile(`afk4-overview-sales-${exportStamp}.csv`, salesCsv, 'text/csv;charset=utf-8');
      setFeedback({ label: t('op.dashboard.export'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.dashboard.export'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const pulseItems = [
    { label: t('op.dashboard.card.cash'), value: formatMinorUnits(totalRevenue.minorUnits, totalRevenue.currencyCode), detail: t('op.dashboard.cashOf', { total: formatMinorUnits(cashTargetMinorUnits, totalRevenue.currencyCode) }), chartValue: cashPercent, chartLabel: <><AnimatedNumber value={cashPercent} />%</>, chartSubLabel: formatCompactNumber(Math.round(minorToMajor(totalRevenue.minorUnits))), tone: 'cash', icon: Banknote },
    { label: t('op.dashboard.pulse.activePcs'), value: `${activePcs} / ${totalPcs}`, detail: t('op.dashboard.forPeriod', { period: activePeriodLabel }), chartValue: Math.round((activePcs / totalPcs) * 100), chartLabel: <><AnimatedNumber value={activePcs} />/{totalPcs}</>, chartSubLabel: t('op.dashboard.now'), tone: 'devices', icon: MonitorCheck },
    { label: t('op.dashboard.pulse.attention'), value: String(attentionCount), detail: t('op.dashboard.attentionDetail', { count: attentionCount, period: activePeriodLabel }), chartValue: Math.min(100, Math.round((attentionCount / Math.max(1, totalPcs * activeDays)) * 100)), chartLabel: <AnimatedNumber value={attentionCount} />, chartSubLabel: t('op.dashboard.signalsShort'), tone: 'attention', icon: ShieldAlert },
    { label: t('op.dashboard.pulse.bookings'), value: `${bookingUsed} / ${bookingSlots}`, detail: t('op.dashboard.slotsForPeriod', { period: activePeriodLabel }), chartValue: bookingSlots > 0 ? Math.min(100, Math.round((bookingUsed / bookingSlots) * 100)) : 0, chartLabel: <><AnimatedNumber value={bookingUsed} />/{bookingSlots}</>, chartSubLabel: t('op.dashboard.slots'), tone: 'booking', icon: CalendarClock }
  ];

  const controlCards: Array<[WorkspaceId, string, string, string, LucideIcon]> = [
    ['map', t('op.dashboard.card.map'), t('op.dashboard.pcs', { count: totalPcs }), t('op.dashboard.signals', { count: attentionCount }), MonitorCheck],
    ['cash', t('op.dashboard.card.sales'), t('op.dashboard.checks', { count: posChecks }), t('op.dashboard.forPeriod', { period: activePeriodLabel }), ReceiptText],
    ['cash', t('op.dashboard.card.cash'), formatMinorUnits(totalRevenue.minorUnits, totalRevenue.currencyCode), t('op.dashboard.forPeriod', { period: activePeriodLabel }), CircleDollarSign],
    ['players', t('op.dashboard.card.client'), t('op.dashboard.newClients', { count: newClients }), t('op.dashboard.forPeriod', { period: activePeriodLabel }), UserRoundPlus]
  ];

  return (
    <section className="workspace-screen dashboard-screen">
      <section className="screen-head dashboard-head">
        <div>
          <span>{t('op.dashboard.overview')}</span>
          <h1>{t('op.dashboard.headline', { range: activeRange.label })}</h1>
        </div>
        <div className="filter-row dashboard-period-filter" aria-label={t('op.dashboard.periodFilterLabel')}>
          <div className="period-segment">
            <button type="button" className={period === 'today' ? 'active' : undefined} onClick={() => setPeriod('today')}>{t('op.dashboard.period.today')}</button>
            <button type="button" className={period === 'week' ? 'active' : undefined} onClick={() => setPeriod('week')}>{t('op.dashboard.period.week')}</button>
            <button type="button" className={period === 'month' ? 'active' : undefined} onClick={() => setPeriod('month')}>{t('op.dashboard.period.month')}</button>
          </div>
          <div className={`date-range-control ${period === 'custom' ? 'active' : ''}`}>
            <label>
              <span>{t('op.dashboard.dateFrom')}</span>
              <input
                type="date"
                aria-label={t('op.dashboard.dateFromLabel')}
                value={customRange.from}
                onChange={(event) => updateCustomRange('from', event.currentTarget.value)}
                onInput={(event) => updateCustomRange('from', event.currentTarget.value)}
                onFocus={() => setPeriod('custom')}
              />
            </label>
            <label>
              <span>{t('op.dashboard.dateTo')}</span>
              <input
                type="date"
                aria-label={t('op.dashboard.dateToLabel')}
                value={customRange.to}
                onChange={(event) => updateCustomRange('to', event.currentTarget.value)}
                onInput={(event) => updateCustomRange('to', event.currentTarget.value)}
                onFocus={() => setPeriod('custom')}
              />
            </label>
            <span className="date-range-days" aria-label={t('op.dashboard.periodLengthLabel', { days: periodDaysShort })}>{periodDaysShort}</span>
          </div>
          <span className={`map-load-state ${dashboardLoadStatus === 'backend' ? 'ready' : dashboardLoadStatus}`}>{dashboardStatusText}</span>
          <button type="button" className="export-button" aria-label={t('op.dashboard.exportLabel', { range: exportLabel })} onClick={exportDashboard}>
            {t('op.dashboard.export')}
          </button>
        </div>
      </section>

      {showDashboardSkeleton ? (
        <section className="dashboard-layout" role="status" aria-label={dashboardStatusText}>
          <Skeleton className="dashboard-skeleton-now" />
          <Skeleton className="dashboard-skeleton-queue" />
          <Skeleton className="dashboard-skeleton-control" />
          <Skeleton className="dashboard-skeleton-pulse" />
        </section>
      ) : (
      <section className="dashboard-layout">
        <article className="dashboard-now-panel">
          <header className="dashboard-panel-title">
            <span>{t('op.dashboard.mainFocus')}</span>
            <strong>{selectedFocus[2]}</strong>
          </header>
          <p>{selectedFocus[3]}</p>
          <div className="dashboard-now-meta">
            <span><AlertTriangle size={15} /> {selectedFocus[0]}</span>
            <span>{selectedFocus[1]}</span>
            <span>{dashboardStatusText}</span>
          </div>
          <div className="dashboard-now-actions">
            <button type="button" onClick={() => openSelectedFocusSeat(t('op.dashboard.resolve'))}><AlertTriangle size={15} /> {t('op.dashboard.resolve')}</button>
            <button type="button" onClick={() => openSelectedFocusSeat(t('op.map.pcControlLabel'))}><Wrench size={15} /> {t('op.map.pcControlLabel')}</button>
          </div>
          {dashboardLoadStatus === 'failed' && (
            <p className="workspace-error" role="alert">{dashboardLoadError ?? t('op.dashboard.unavailable')}</p>
          )}
        </article>

        <section className="dashboard-secondary-panel">
          <header className="dashboard-panel-title">
            <span>{t('op.dashboard.nextInQueue')}</span>
            <strong>{t('op.dashboard.afterCritical')}</strong>
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
            <span>{t('op.dashboard.control')}</span>
            <strong>{t('op.dashboard.controlHint')}</strong>
          </header>
          <div className="dashboard-control-grid">
            {controlCards.map(([targetWorkspace, label, value, detail, Icon]) => (
              <DashboardControlCard
                key={label}
                label={label}
                value={value}
                detail={detail}
                icon={Icon}
                onActivate={() => onNavigate(targetWorkspace)}
              />
            ))}
          </div>
        </section>

        <section className="dashboard-pulse-panel">
          <header className="dashboard-panel-title">
            <span>{t('op.dashboard.shiftPulse')}</span>
            <strong>{t('op.dashboard.shiftPulseHint')}</strong>
          </header>
          <div className="dashboard-pulse-grid">
            {pulseItems.map((item) => (
              <DashboardPulseCard key={item.label} {...item} />
            ))}
          </div>
        </section>
      </section>
      )}
    </section>
  );
}
