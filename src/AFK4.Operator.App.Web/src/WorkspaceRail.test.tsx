import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import { WorkspaceRail } from './WorkspaceRail';

afterEach(cleanup);

const shift = { tone: 'idle', value: '—', full: 'Смена не открыта' } as const;

function renderRail(permissions: string[]) {
  const session = {
    staffUserId: 'staff-1',
    organizationId: 'org-1',
    displayName: 'Тестовый сотрудник',
    accessToken: 'token',
    accessTokenExpiresAtUtc: '2026-07-15T10:00:00Z',
    refreshTokenExpiresAtUtc: '2026-07-16T10:00:00Z',
    branchIds: ['branch-1'],
    activeBranchId: 'branch-1',
    permissions
  } satisfies OperatorAuthSession;

  render(
    <I18nProvider>
      <WorkspaceRail
        session={session}
        activeSectionKey="map"
        displayName={session.displayName}
        shift={shift}
        onNavigateSection={() => {}}
        onOpenAccount={() => {}}
        onSignOut={() => {}}
      />
    </I18nProvider>
  );
}

describe('WorkspaceRail permissions', () => {
  it('hides sections with no permitted workspace', () => {
    renderRail(['floor_map.view']);

    expect(screen.getByTitle('Карта')).toBeInTheDocument();
    expect(screen.queryByTitle('Касса')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Отчёты')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Управление')).not.toBeInTheDocument();
  });

  it('keeps a section when one nested workspace is permitted', () => {
    // devices.commands.dispatch is one of several permissions behind the single `management`
    // workspace item (see managementNav.ts "Залы и ПК" destination) — diagnostics.view no longer
    // qualifies since audit/logs left `Управление` in the redesign (2026-07-16 design doc). Note
    // read-only devices.detail.view/devices.commands.status.view do NOT qualify (Task 1.5 fix):
    // they gate device-info controls inside the layout screen, not whole-section visibility.
    renderRail(['devices.commands.dispatch']);

    expect(screen.getByTitle('Управление')).toBeInTheDocument();
  });
});
