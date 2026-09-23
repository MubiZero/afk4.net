import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationClubsTab } from './OrganizationClubsTab';
import type { OrganizationBranch, OrganizationLimits } from '@/api/types';

function branch(overrides: Partial<OrganizationBranch> = {}): OrganizationBranch {
  return {
    branchId: 'branch-1',
    slug: 'main',
    name: 'Главный клуб',
    city: 'Душанбе',
    createdAtUtc: '2026-01-01T00:00:00Z',
    ...overrides
  };
}

function limits(overrides: Partial<OrganizationLimits> = {}): OrganizationLimits {
  return { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null, ...overrides };
}

function pulseClient() {
  return { getPulse: mock().mockResolvedValue({ generatedAtUtc: '2026-01-01T00:00:00Z', organizations: [] }) };
}

function organizationsClient() {
  return { createBranch: mock() };
}

describe('OrganizationClubsTab', () => {
  it('показывает занятость лимита филиалов', async () => {
    const branches = [branch({ branchId: 'branch-1' }), branch({ branchId: 'branch-2', slug: 'north' })];
    render(
      <I18nProvider>
        <OrganizationClubsTab
          client={pulseClient()}
          organizationsClient={organizationsClient()}
          organizationId="org-1"
          branches={branches}
          limits={limits({ maxBranches: 3 })}
          canAddBranch
          onBranchCreated={mock()}
        />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('Филиалов: 2 из 3')).toBeInTheDocument());
  });

  it('не показывает счётчик, если лимит филиалов не задан', async () => {
    const branches = [branch()];
    render(
      <I18nProvider>
        <OrganizationClubsTab
          client={pulseClient()}
          organizationsClient={organizationsClient()}
          organizationId="org-1"
          branches={branches}
          limits={limits({ maxBranches: null })}
          canAddBranch
          onBranchCreated={mock()}
        />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('Главный клуб')).toBeInTheDocument());
    expect(screen.queryByText(/^Филиалов:/)).not.toBeInTheDocument();
  });

  // Без филиала владельцу не выдать код приглашения: пустая вкладка — это первый шаг
  // онбординга, а говорила она «Клубы этой организации не найдены в пульсе».
  it('организация без филиалов зовёт добавить первый', async () => {
    render(
      <I18nProvider><ToastProvider>
        <OrganizationClubsTab
          client={pulseClient()}
          organizationsClient={organizationsClient()}
          organizationId="org-1"
          branches={[]}
          limits={limits()}
          canAddBranch
          onBranchCreated={mock()}
        />
      </ToastProvider></I18nProvider>
    );

    fireEvent.click(screen.getByRole('button', { name: 'Добавить первый филиал' }));

    expect(await screen.findByRole('dialog', { name: 'Новый филиал' })).toBeInTheDocument();
  });

  // Филиал заводится по праву на заведение организаций: кнопка у того, кому сервер откажет,
  // хуже её отсутствия.
  it('без права заводить филиалы кнопок нет, а строка говорит, у кого оно есть', () => {
    render(
      <I18nProvider>
        <OrganizationClubsTab
          client={pulseClient()}
          organizationsClient={organizationsClient()}
          organizationId="org-1"
          branches={[]}
          limits={limits()}
          canAddBranch={false}
          onBranchCreated={mock()}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Это может сотрудник платформы с правом «Заводить новые клубы».')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Добавить первый филиал' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Добавить филиал' })).toBeNull();
  });
});
