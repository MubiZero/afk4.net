import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { HealthScreen } from './HealthScreen';
import type { HealthOverview, Incident, JobHealth, QueueHealth } from '@/api/types';

function job(overrides: Partial<JobHealth> = {}): JobHealth {
  return {
    jobName: 'invoice_generation',
    lastRunAtUtc: '2026-08-01T00:00:00Z',
    lastSuccessAtUtc: '2026-08-01T00:00:00Z',
    lastOutcome: 'success',
    lastItemsProcessed: 3,
    lastError: null,
    consecutiveFailures: 0,
    ...overrides
  };
}

function incident(overrides: Partial<Incident> = {}): Incident {
  return {
    incidentId: 'i1',
    kind: 'job_overdue',
    dedupKey: 'job_overdue:invoice_generation',
    severity: 'critical',
    detailsJson: null,
    openedAtUtc: '2026-08-01T00:00:00Z',
    lastSeenAtUtc: '2026-08-01T00:00:00Z',
    ...overrides
  };
}

function queue(overrides: Partial<QueueHealth> = {}): QueueHealth {
  return { queueName: 'notifications', pendingCount: 0, failedCount: 0, stuckCount: 0, ...overrides };
}

function overview(overrides: Partial<HealthOverview> = {}): HealthOverview {
  return { generatedAtUtc: '2026-08-07T00:00:00Z', jobs: [job()], queues: [queue()], openIncidents: [], recentFailures: [], mediaStorageConfigured: true, alertSmsConfigured: true, ...overrides };
}

function fakeClient(result: HealthOverview | (() => Promise<HealthOverview>)) {
  return {
    getOverview: typeof result === 'function' ? mock(result) : mock().mockResolvedValue(result),
    sendTestEmail: mock().mockResolvedValue({ delivered: true, error: null })
  };
}

describe('HealthScreen', () => {
  it('shows a critical incident with its translated title and severity label', async () => {
    render(
      <I18nProvider>
        <HealthScreen canSendTestEmail client={fakeClient(overview({ openIncidents: [incident({ kind: 'job_overdue', severity: 'critical' })] }))} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Задание не отрабатывает')).toBeInTheDocument());
    expect(screen.getAllByText('Критично').length).toBeGreaterThan(0);
  });

  it('shows the empty-incidents copy and no incident titles when there are none', async () => {
    render(
      <I18nProvider>
        <HealthScreen canSendTestEmail client={fakeClient(overview({ openIncidents: [] }))} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Открытых проблем нет')).toBeInTheDocument());
    expect(screen.queryByText('Задание не отрабатывает')).not.toBeInTheDocument();
  });

  it('shows an error state with retry, never the "no incidents" copy, when the load fails', async () => {
    render(
      <I18nProvider>
        <HealthScreen canSendTestEmail client={fakeClient(() => Promise.reject(new Error('network')))} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.getByText('Повторить')).toBeInTheDocument();
    expect(screen.queryByText('Открытых проблем нет')).not.toBeInTheDocument();
  });
});

// Счётчик «провалено» без причины отправлял админа в базу — ради этой строки секция и живёт.
it('shows why a queued message failed', async () => {
  render(
    <I18nProvider><HealthScreen canSendTestEmail client={fakeClient(overview({
      recentFailures: [{
        queueName: 'notifications',
        failedAtUtc: '2026-08-07T10:00:00Z',
        kind: 'owner.invite',
        recipientMasked: 'i***@mubi.dev',
        attemptCount: 1,
        lastError: 'Notification SMTP FromAddress is not configured.'
      }]
    }))} /></I18nProvider>
  );

  expect(await screen.findByText('Notification SMTP FromAddress is not configured.')).toBeTruthy();
  expect(screen.getByText(/i\*\*\*@mubi\.dev/)).toBeTruthy();
});

it('says so when there are no recent failures', async () => {
  render(
    <I18nProvider><HealthScreen canSendTestEmail client={fakeClient(overview({ recentFailures: [] }))} /></I18nProvider>
  );
  expect(await screen.findByText('Свежих провалов нет')).toBeTruthy();
});

// Кнопка существует ради текста ошибки: без него «письмо не ушло» отправляет разбираться в базу.
it('shows the delivery error when the test email fails', async () => {
  const client = {
    getOverview: mock().mockResolvedValue(overview({})),
    sendTestEmail: mock().mockResolvedValue({ delivered: false, error: 'Connection refused' })
  };
  render(<I18nProvider><HealthScreen client={client} canSendTestEmail /></I18nProvider>);

  fireEvent.change(await screen.findByLabelText('Куда отправить'), { target: { value: 'me@mubi.dev' } });
  fireEvent.click(screen.getByRole('button', { name: 'Отправить проверочное письмо' }));

  expect(await screen.findByText('Connection refused')).toBeTruthy();
  expect(client.sendTestEmail).toHaveBeenCalledWith('me@mubi.dev');
});

it('confirms a delivered test email', async () => {
  const client = {
    getOverview: mock().mockResolvedValue(overview({})),
    sendTestEmail: mock().mockResolvedValue({ delivered: true, error: null })
  };
  render(<I18nProvider><HealthScreen client={client} canSendTestEmail /></I18nProvider>);

  fireEvent.change(await screen.findByLabelText('Куда отправить'), { target: { value: 'me@mubi.dev' } });
  fireEvent.click(screen.getByRole('button', { name: 'Отправить проверочное письмо' }));

  expect(await screen.findByText('Письмо отправлено')).toBeTruthy();
});

// Про ненастроенное хранилище узнать должны мы, а не клуб при первой загрузке логотипа.
it('warns when file storage is not configured', async () => {
  render(<I18nProvider><HealthScreen canSendTestEmail client={fakeClient(overview({ mediaStorageConfigured: false }))} /></I18nProvider>);

  expect(await screen.findByText(/Не настроено: логотипы и фото зала/)).toBeTruthy();
});

// «Почта умерла — придёт SMS» должно быть правдой. Пока шаблон у шлюза не заведён, канал молчит,
// и узнать об этом надо до аварии, а не по ней самой.
it('говорит, когда резервный канал оповещений молчит', async () => {
  render(<I18nProvider><HealthScreen canSendTestEmail client={fakeClient(overview({ alertSmsConfigured: false }))} /></I18nProvider>);

  expect(await screen.findByText(/Критические оповещения уходят только письмом/)).toBeTruthy();
});

it('настроенный канал оповещений карточкой не шумит', async () => {
  render(<I18nProvider><HealthScreen canSendTestEmail client={fakeClient(overview())} /></I18nProvider>);

  await screen.findByText('Задания');
  expect(screen.queryByText(/Критические оповещения уходят только письмом/)).toBeNull();
});

// Отправку проверочного письма бэкенд спрашивает по отдельному праву. Пока карточка не имела
// своего гейта, кнопка была активна у того, кому нельзя, и отвечала только отказом.
it('hides the test email card without the permission', async () => {
  render(
    <I18nProvider>
      <HealthScreen client={fakeClient(overview({}))} canSendTestEmail={false} />
    </I18nProvider>
  );

  expect(await screen.findByText('Здоровье платформы')).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Отправить проверочное письмо' })).toBeNull();
});
