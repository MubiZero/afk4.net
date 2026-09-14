import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { ErrorState, LoadingCards } from '@/components/ui/states';
import { Page } from '@/components/layout/Page';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import type { HealthApi } from '@/api/platformClients/health';
import type { HealthOverview, IncidentSeverity, JobHealth, QueueFailure, QueueHealth } from '@/api/types';
import { useHealth } from './useHealth';
import { hasCritical, jobStatus, sortIncidents } from './healthModel';

// Значение с сервера может опережать релиз панели (новый вид задания/инцидента/очереди уже
// пишется в БД, а перевод для него ещё не завезли) — для незнакомого значения показываем его
// как есть, а не роняем экран пустотой.
const JOB_LABEL_KEYS: Record<string, MessageKey> = {
  invoice_generation: 'platform.health.job.invoice_generation',
  billing_outbox: 'platform.health.job.billing_outbox',
  notification_dispatch: 'platform.health.job.notification_dispatch',
  daily_summary: 'platform.health.job.daily_summary',
  scheduled_reports: 'platform.health.job.scheduled_reports',
  auto_protection: 'platform.health.job.auto_protection',
  health_watch: 'platform.health.job.health_watch',
  alert_delivery: 'platform.health.job.alert_delivery'
};

const INCIDENT_LABEL_KEYS: Record<string, MessageKey> = {
  job_overdue: 'platform.health.incident.job_overdue',
  job_failing: 'platform.health.incident.job_failing',
  notification_queue_stuck: 'platform.health.incident.notification_queue_stuck',
  billing_outbox_stuck: 'platform.health.incident.billing_outbox_stuck',
  notification_queue_failing: 'platform.health.incident.notification_queue_failing',
  billing_outbox_failing: 'platform.health.incident.billing_outbox_failing'
};

const QUEUE_LABEL_KEYS: Record<string, MessageKey> = {
  notifications: 'platform.health.queue.notifications',
  billing_outbox: 'platform.health.queue.billing_outbox'
};

const SEVERITY_LABEL_KEYS: Record<IncidentSeverity, MessageKey> = {
  warning: 'platform.health.severity.warning',
  critical: 'platform.health.severity.critical'
};

export interface HealthScreenProps {
  client: Pick<HealthApi, 'getOverview' | 'sendTestEmail'>;
  canSendTestEmail: boolean;
}

export function HealthScreen({ client, canSendTestEmail }: HealthScreenProps) {
  const { t } = useI18n();
  const state = useHealth(client);

  return (
    <Page title={t('platform.health.title')} description={t('platform.health.subtitle')}>
      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <>
          <HealthOverviewView overview={state.data} />
          {canSendTestEmail && <TestEmailCard client={client} />}
        </>
      )}
    </Page>
  );
}

// Проверка доставки боевым путём. Без неё единственным способом убедиться, что почта работает,
// было послать живому человеку настоящее письмо и гадать по счётчику провалов.
function TestEmailCard({ client }: { client: Pick<HealthApi, 'sendTestEmail'> }) {
  const { t } = useI18n();
  const [email, setEmail] = useState('');
  const [sending, setSending] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; error: string | null } | null>(null);

  async function send() {
    if (email.trim() === '') return;
    setSending(true);
    setResult(null);
    try {
      const outcome = await client.sendTestEmail(email.trim());
      setResult({ ok: outcome.delivered, error: outcome.error });
    } catch {
      setResult({ ok: false, error: null });
    } finally {
      setSending(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.health.testEmail.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <CardDescription>{t('platform.health.testEmail.hint')}</CardDescription>
        <label className="ui-field">
          <span>{t('platform.health.testEmail.address')}</span>
          <Input
            type="email"
            aria-label={t('platform.health.testEmail.address')}
            value={email}
            onChange={event => setEmail(event.target.value)}
          />
        </label>
        <Button onClick={() => void send()} disabled={sending || email.trim() === ''}>
          {t('platform.health.testEmail.send')}
        </Button>
        {result === null ? null : result.ok ? (
          <Badge variant="success">{t('platform.health.testEmail.sent')}</Badge>
        ) : (
          <span className="pc-queue-id">
            <Badge variant="destructive">{t('platform.health.testEmail.failed')}</Badge>
            {/* Причина отказа от канала доставки — ради неё эта кнопка и существует. */}
            {result.error === null ? null : <span>{result.error}</span>}
          </span>
        )}
      </CardContent>
    </Card>
  );
}

function HealthOverviewView({ overview }: { overview: HealthOverview }) {
  const { t } = useI18n();
  return (
    <>
      <IncidentsCard overview={overview} />
      <JobsCard jobs={overview.jobs} />
      <QueuesCard queues={overview.queues} />
      <FailuresCard failures={overview.recentFailures} />
      {overview.mediaStorageConfigured ? null : (
        <Card>
          <CardHeader>
            <CardTitle>{t('platform.health.mediaStorage.title')}</CardTitle>
          </CardHeader>
          <CardContent>
            <CardDescription>{t('platform.health.mediaStorage.missing')}</CardDescription>
          </CardContent>
        </Card>
      )}
    </>
  );
}

function IncidentsCard({ overview }: { overview: HealthOverview }) {
  const { t, formatDate } = useI18n();
  const incidents = sortIncidents(overview.openIncidents);
  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{t('platform.health.incidents.title')}</CardTitle>
          {hasCritical(overview) ? <Badge variant="destructive">{t('platform.health.severity.critical')}</Badge> : null}
        </div>
      </CardHeader>
      <CardContent>
        {incidents.length === 0 ? (
          <CardDescription>{t('platform.health.incidents.empty')}</CardDescription>
        ) : (
          <ul className="pc-queue">
            {incidents.map(incident => (
              <li key={incident.incidentId} className="pc-queue-row" data-testid="incident-row">
                <span className="pc-queue-id">
                  <strong>{translate(t, INCIDENT_LABEL_KEYS, incident.kind)}</strong>
                  <span>{formatDate(incident.openedAtUtc)}</span>
                </span>
                <Badge variant={incident.severity === 'critical' ? 'destructive' : 'warning'}>
                  {translateSeverity(t, incident.severity)}
                </Badge>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

// Счётчик «провалено» без причины нечитаем: видно, что не уходит, и не видно почему.
// Разбор проваленной доставки начинается отсюда, а не с прямого доступа к базе.
function FailuresCard({ failures }: { failures: QueueFailure[] }) {
  const { t, formatDate } = useI18n();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.health.failures.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        {failures.length === 0 ? (
          <CardDescription>{t('platform.health.failures.empty')}</CardDescription>
        ) : (
          <ul className="pc-queue">
            {failures.map((failure, index) => (
              <li key={`${failure.queueName}-${failure.failedAtUtc ?? index}`} className="pc-queue-row" data-testid="failure-row">
                <span className="pc-queue-id">
                  <strong>{translate(t, QUEUE_LABEL_KEYS, failure.queueName)}</strong>
                  <span>
                    {failure.kind}
                    {failure.recipientMasked === '' ? '' : ` · ${failure.recipientMasked}`}
                  </span>
                  {/* Техническая строка от канала доставки — экран под правом платформенной роли. */}
                  {failure.lastError === null ? null : <span>{failure.lastError}</span>}
                </span>
                <span className="pc-cell-actions">
                  <Badge variant="secondary">
                    {t('platform.health.failures.attempts', { count: failure.attemptCount })}
                  </Badge>
                  {failure.failedAtUtc === null ? null : (
                    <Badge variant="destructive">{formatDate(failure.failedAtUtc)}</Badge>
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}

function JobsCard({ jobs }: { jobs: JobHealth[] }) {
  const { t, formatDate } = useI18n();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.health.jobs.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="pc-queue">
          {jobs.map(job => {
            const status = jobStatus(job);
            const detail = status === 'never'
              ? t('platform.health.neverRan')
              : status === 'failing'
                ? t('platform.health.failureStreak', { count: job.consecutiveFailures })
                : job.lastSuccessAtUtc !== null
                  ? t('platform.health.lastSuccess', { time: formatDate(job.lastSuccessAtUtc) })
                  : t('platform.health.neverRan');
            return (
              <li key={job.jobName} className="pc-queue-row" data-testid="job-row">
                <span className="pc-queue-id">
                  <strong>{translate(t, JOB_LABEL_KEYS, job.jobName)}</strong>
                  {/* lastError — сырое техническое сообщение исключения; экран под правом
                      платформенной роли, показывать можно, но как техническую строку. */}
                  {status === 'failing' && job.lastError !== null ? <span>{job.lastError}</span> : null}
                </span>
                <Badge variant={status === 'failing' ? 'destructive' : status === 'never' ? 'secondary' : 'success'}>
                  {detail}
                </Badge>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}

function QueuesCard({ queues }: { queues: QueueHealth[] }) {
  const { t } = useI18n();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.health.queues.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="pc-queue">
          {queues.map(queue => (
            <li key={queue.queueName} className="pc-queue-row" data-testid="queue-row">
              <span className="pc-queue-id">
                <strong>{translate(t, QUEUE_LABEL_KEYS, queue.queueName)}</strong>
              </span>
              <span className="pc-cell-actions">
                <Badge variant="secondary">{t('platform.health.queue.pending', { count: queue.pendingCount })}</Badge>
                <Badge variant={queue.failedCount > 0 ? 'destructive' : 'secondary'}>
                  {t('platform.health.queue.failed', { count: queue.failedCount })}
                </Badge>
                <Badge variant={queue.stuckCount > 0 ? 'destructive' : 'secondary'}>
                  {t('platform.health.queue.stuck', { count: queue.stuckCount })}
                </Badge>
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

function translate(t: (key: MessageKey, values?: Record<string, string | number>) => string, table: Record<string, MessageKey>, value: string): string {
  const key = table[value];
  return key !== undefined ? t(key) : value;
}

// `severity` is typed as the known union, but it originates from the server response —
// an unrecognized value must fall back to the raw string, same as job/incident/queue names.
function translateSeverity(t: (key: MessageKey, values?: Record<string, string | number>) => string, severity: IncidentSeverity): string {
  return translate(t, SEVERITY_LABEL_KEYS, severity);
}
