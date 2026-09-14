import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { projectOperatorError } from '../apiErrors';
import type { ReportScheduleDto } from '../api/clients/reports';
import type { OperatorBackendContext } from '../operatorTypes';
import { createReportClients } from './reportClient';

/**
 * Регулярные отчёты на почту владельца клуба.
 *
 * Сервер умел это целиком — расписание, фоновая сборка CSV, отправка письма, — и не имел ни
 * одного клиента: завести рассылку можно было только запросом к API руками. Поэтому здесь нет
 * ничего, кроме трёх действий, которые сервер уже поддерживает: посмотреть, завести, убрать.
 * Приостановки нет намеренно — её нет и на сервере, и кнопка «выключить», которая на деле удаляет,
 * врала бы.
 */
const REPORT_TYPES: { value: string; labelKey: MessageKey }[] = [
  { value: 'shifts', labelKey: 'op.reports.schedule.type.shifts' },
  { value: 'sales', labelKey: 'op.reports.schedule.type.sales' },
  { value: 'gameplay_time', labelKey: 'op.reports.schedule.type.gameplayTime' },
  { value: 'cash_operations', labelKey: 'op.reports.schedule.type.cashOperations' },
  { value: 'operator_actions', labelKey: 'op.reports.schedule.type.operatorActions' }
];

const FREQUENCIES: { value: string; labelKey: MessageKey }[] = [
  { value: 'daily', labelKey: 'op.reports.schedule.frequency.daily' },
  { value: 'weekly', labelKey: 'op.reports.schedule.frequency.weekly' },
  { value: 'monthly', labelKey: 'op.reports.schedule.frequency.monthly' }
];

export function reportTypeLabelKey(value: string): MessageKey {
  return REPORT_TYPES.find((type) => type.value === value)?.labelKey ?? 'op.reports.schedule.type.unknown';
}

export function frequencyLabelKey(value: string): MessageKey {
  return FREQUENCIES.find((item) => item.value === value)?.labelKey ?? 'op.reports.schedule.frequency.unknown';
}

/** Уже заведённые сочетания «отчёт + частота»: второе такое же письмо владельцу не нужно. */
export function scheduledPairs(schedules: ReportScheduleDto[]): Set<string> {
  return new Set(schedules.map((schedule) => `${schedule.reportType}:${schedule.frequency}`));
}

export function ReportSchedules({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [schedules, setSchedules] = useState<ReportScheduleDto[]>([]);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');
  const [reportType, setReportType] = useState(REPORT_TYPES[0].value);
  const [frequency, setFrequency] = useState(FREQUENCIES[0].value);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState('');

  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(t('op.reports.backendRequired')); return; }
    setState('loading');
    try {
      setSchedules(await createReportClients(backend).listReportSchedules(backend.branchId));
      setState('ready');
    } catch (reason) {
      setError(projectOperatorError(reason, t).detail);
      setState('error');
    }
  }, [backend, t]);

  useEffect(() => { void load(); }, [load]);

  const existing = scheduledPairs(schedules);
  const alreadyScheduled = existing.has(`${reportType}:${frequency}`);

  async function create() {
    if (!backend || alreadyScheduled) return;
    setBusy(true);
    setActionError('');
    try {
      await createReportClients(backend).createReportSchedule(backend.branchId, {
        organizationId: backend.session.organizationId,
        reportType,
        frequency
      });
      await load();
    } catch (reason) {
      setActionError(projectOperatorError(reason, t).detail);
    } finally {
      setBusy(false);
    }
  }

  async function remove(schedule: ReportScheduleDto) {
    if (!backend) return;
    setBusy(true);
    setActionError('');
    try {
      await createReportClients(backend).deleteReportSchedule(backend.branchId, schedule.reportScheduleId);
      await load();
    } catch (reason) {
      setActionError(projectOperatorError(reason, t).detail);
    } finally {
      setBusy(false);
    }
  }

  return (
    <ManagementScreen
      title={t('op.reports.schedule.title')}
      subtitle={t('op.reports.schedule.subtitle')}
      contentWidth="form"
      state={state}
      errorDetail={error}
      onRetry={() => void load()}
    >
      <div className="mgmt-form">
        <label>
          {t('op.reports.schedule.reportLabel')}
          <select value={reportType} disabled={busy} onChange={(event) => setReportType(event.currentTarget.value)}>
            {REPORT_TYPES.map((type) => <option key={type.value} value={type.value}>{t(type.labelKey)}</option>)}
          </select>
        </label>
        <label>
          {t('op.reports.schedule.frequencyLabel')}
          <select value={frequency} disabled={busy} onChange={(event) => setFrequency(event.currentTarget.value)}>
            {FREQUENCIES.map((item) => <option key={item.value} value={item.value}>{t(item.labelKey)}</option>)}
          </select>
        </label>
        {alreadyScheduled && <p className="mgmt-drawer-hint">{t('op.reports.schedule.duplicate')}</p>}
        {actionError && <p role="alert">{actionError}</p>}
        <button type="button" className="ui-btn ui-btn--primary" disabled={busy || alreadyScheduled} onClick={() => void create()}>
          {t('op.reports.schedule.create')}
        </button>
      </div>

      {schedules.length === 0 ? (
        <p className="mgmt-drawer-hint">{t('op.reports.schedule.empty')}</p>
      ) : (
        <ul>
          {schedules.map((schedule) => (
            <li key={schedule.reportScheduleId} className="mgmt-zone-row">
              <span>
                {t(reportTypeLabelKey(schedule.reportType))} · {t(frequencyLabelKey(schedule.frequency))}
                {' · '}
                {t('op.reports.schedule.nextRun', { time: formatDate(schedule.nextRunUtc) })}
              </span>
              <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => void remove(schedule)}>
                {t('op.reports.schedule.remove')}
              </button>
            </li>
          ))}
        </ul>
      )}
    </ManagementScreen>
  );
}
