import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import type { WorkspaceId } from './operatorTypes';
import { WorkspaceRail } from './WorkspaceRail';
import { supportPermissions, supportVisibleWorkspaces } from './support/supportWorkspaces';

afterEach(cleanup);

const shift = { tone: 'idle', value: '—', full: 'Смена не открыта' } as const;

function renderRail(permissions: string[], visibleWorkspaceIds?: ReadonlySet<WorkspaceId> | null) {
  const session = {
    staffUserId: 'staff-1',
    organizationId: 'org-1',
    displayName: 'Тестовый сотрудник',
    accessToken: 'token',
    accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2026-07-16T10:00:00Z',
    branchIds: ['branch-1'],
    activeBranchId: 'branch-1',
    permissions
  } satisfies OperatorAuthSession;

  render(
    <I18nProvider>
      <WorkspaceRail
        session={session}
        visibleWorkspaceIds={visibleWorkspaceIds}
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
    renderRail(['organization.floor_map.view']);

    expect(screen.getByTitle('Карта')).toBeInTheDocument();
    expect(screen.queryByTitle('Касса')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Отчёты')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Управление')).not.toBeInTheDocument();
  });

  it('keeps a section when one nested workspace is permitted', () => {
    // devices.seat_assignment.assign is one of several permissions behind the single `management`
    // workspace item (see managementNav.ts "Залы и ПК" destination). After the CRUD rework
    // (2026-07-17) enrollment-code/dispatch-command were dropped from that screen, so
    // devices.commands.dispatch no longer qualifies — the gate is now the perms that actually
    // unlock content (layout.manage / seat assignment / credential rotate-revoke). Read-only
    // devices.detail.view/commands.status.view still do NOT grant whole-section visibility.
    renderRail(['organization.devices.seat_assignment.assign']);

    expect(screen.getByTitle('Управление')).toBeInTheDocument();
  });
});

describe('WorkspaceRail under an active support session', () => {
  it('hides money sections even though permissions alone would allow them, and keeps the covered ones', () => {
    const allAreas = ['branch-settings', 'devices', 'staff', 'floor-map', 'branch-profile'];
    // Same permission set supportOperatorSession would build (view* everywhere + writes for every
    // area) — proves the money workspaces stay hidden via visibleWorkspaceIds even when the
    // permission-only check (canOpenWorkspace) would have let them through.
    renderRail(supportPermissions(allAreas), supportVisibleWorkspaces(allAreas));

    expect(screen.getByTitle('Карта')).toBeInTheDocument();
    expect(screen.getByTitle('Управление')).toBeInTheDocument();
    expect(screen.getByTitle('Сеть')).toBeInTheDocument();
    expect(screen.queryByTitle('Касса')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Брони')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Клиенты')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Склад')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Отчёты')).not.toBeInTheDocument();
  });

  it('with no writable areas at all, only permission-gated sections would show — here none, since read-only permissions never unlock a workspace on their own', () => {
    renderRail(supportPermissions([]), supportVisibleWorkspaces([]));

    // viewFloorMap alone doesn't satisfy workspacePermissionRules.map (needs the same permission,
    // which IS a view permission — map is actually gated by viewFloorMap, so it stays visible by
    // permission, but visibleWorkspaceIds (empty set) must still hide it: the money-hiding
    // restriction is a HARD floor under whatever the permission check alone would allow.
    expect(screen.queryByTitle('Карта')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Управление')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Сеть')).not.toBeInTheDocument();
  });
});
