import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());
// mock.module leaks process-wide and mutates already-imported namespace objects in place (see
// src/test/setup.ts and ManagementWorkspace.test.tsx) — restore to the pristine pre-suite
// snapshot afterward so HistoryDestination.test.tsx/BranchJournalDestination.test.tsx (which
// render the real components) aren't left seeing these stubs when the whole suite runs in one
// bun process.
afterAll(() => {
  mock.module('./overview/OverviewDestination', () => (globalThis as typeof globalThis & {
    __afk4RealReportsOverviewDestination: typeof import('./overview/OverviewDestination');
  }).__afk4RealReportsOverviewDestination);
  mock.module('./history/HistoryDestination', () => (globalThis as typeof globalThis & {
    __afk4RealReportsHistoryDestination: typeof import('./history/HistoryDestination');
  }).__afk4RealReportsHistoryDestination);
  mock.module('./journal/BranchJournalDestination', () => (globalThis as typeof globalThis & {
    __afk4RealReportsJournalDestination: typeof import('./journal/BranchJournalDestination');
  }).__afk4RealReportsJournalDestination);
});

// Заглушки трёх destination — проверяем сам switcher (навигация/видимость), не их внутренности.
mock.module('./overview/OverviewDestination', () => ({ OverviewDestination: () => <div>OVERVIEW</div> }));
mock.module('./history/HistoryDestination', () => ({ HistoryDestination: () => <div>HISTORY</div> }));
mock.module('./journal/BranchJournalDestination', () => ({ BranchJournalDestination: () => <div>JOURNAL</div> }));

function backendWith(permissions: string[]) {
  return { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { permissions, accessToken: 't', organizationId: 'org' }, branchId: 'b1' } as never;
}

describe('ReportsWorkspace', () => {
  it('renders first allowed destination (overview) by default', async () => {
    const { ReportsWorkspace } = await import('./ReportsWorkspace');
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['organization.reports.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.getByText('OVERVIEW')).toBeInTheDocument();
  });

  it('shows only journal when session has audit.view alone', async () => {
    const { ReportsWorkspace } = await import('./ReportsWorkspace');
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['organization.audit.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.getByText('JOURNAL')).toBeInTheDocument();
    expect(screen.queryByText('OVERVIEW')).not.toBeInTheDocument();
  });

  it('renders no-access when session lacks both permissions', async () => {
    const { ReportsWorkspace } = await import('./ReportsWorkspace');
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith([])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.queryByText('OVERVIEW')).not.toBeInTheDocument();
    expect(screen.queryByText('HISTORY')).not.toBeInTheDocument();
    expect(screen.queryByText('JOURNAL')).not.toBeInTheDocument();
  });
});
