import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => {
  cleanup();
  searchOrganizationAudit.mockClear();
  auditRecords = [{
    auditRecordId: 'r1', branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
    action: 'news.published', targetType: 'News', targetId: 'n1', outcome: 'Succeeded',
    sourceApp: 'PlatformApi', detailsJson: '{}', createdAtUtc: '2026-07-20T10:00:00Z'
  }];
  auditFails = false;
});

let auditRecords: unknown[] = [{
  auditRecordId: 'r1', branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
  action: 'news.published', targetType: 'News', targetId: 'n1', outcome: 'Succeeded',
  sourceApp: 'PlatformApi', detailsJson: '{}', createdAtUtc: '2026-07-20T10:00:00Z'
}];
let auditFails = false;

const searchOrganizationAudit = mock(async () => {
  if (auditFails) throw new Error('boom');
  return { records: auditRecords, limit: 100 };
});

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgAudit: { searchOrganizationAudit }
  })
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('JournalDestination', () => {
  it('renders an audit row including an org-level (null-branch) action', async () => {
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText('news.published')).toBeInTheDocument());
    // The whole point of this screen is org-wide audit — verify the org-level (BranchId=null)
    // record actually reached the query the client was called with, not just that some row rendered.
    expect(searchOrganizationAudit).toHaveBeenCalled();
    expect(screen.getByText('News (n1)')).toBeInTheDocument();
    expect(screen.getByText('система')).toBeInTheDocument();
  });

  // Фильтры бесполезны, если выбранное не доезжает до запроса. Покрытие перенесено сюда с
  // отключённого теста прежнего экрана «Логи»: там оно было единственным на этот путь.
  it('переносит выбранные фильтры в запрос к серверу', async () => {
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(searchOrganizationAudit).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'sessions.start' } });
    fireEvent.change(screen.getByLabelText('Тип объекта'), { target: { value: 'Session' } });
    fireEvent.change(screen.getByLabelText('Итог'), { target: { value: 'Denied' } });
    fireEvent.click(screen.getByRole('button', { name: 'Применить' }));

    await waitFor(() => {
      const last = searchOrganizationAudit.mock.calls.at(-1) as unknown as [string, Record<string, unknown>];
      expect(last[1]).toMatchObject({ action: 'sessions.start', targetType: 'Session', outcome: 'Denied' });
    });
  });

  // «Сбросить» обязан убрать поля из запроса, а не отправить пустые строки: сервер по пустому
  // action фильтровал бы по пустоте вместо «любое действие».
  it('сброс убирает фильтры из запроса, а не шлёт пустые строки', async () => {
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(searchOrganizationAudit).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'sessions.start' } });
    fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
    await waitFor(() => {
      const last = searchOrganizationAudit.mock.calls.at(-1) as unknown as [string, Record<string, unknown>];
      expect(last[1]).toMatchObject({ action: 'sessions.start' });
    });

    fireEvent.click(screen.getByRole('button', { name: 'Сбросить' }));

    await waitFor(() => {
      const last = searchOrganizationAudit.mock.calls.at(-1) as unknown as [string, Record<string, unknown>];
      expect(last[1].action).toBeUndefined();
      expect(last[1].outcome).toBeUndefined();
    });
  });

  it('пустой ответ показывает «Записей нет», а не пустую таблицу', async () => {
    auditRecords = [];
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);

    expect(await screen.findByText('Записей нет')).toBeInTheDocument();
  });

  it('сбой загрузки предлагает повтор, а не выглядит как пустой журнал', async () => {
    auditFails = true;
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);

    expect(await screen.findByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.queryByText('Записей нет')).toBeNull();
  });
});
