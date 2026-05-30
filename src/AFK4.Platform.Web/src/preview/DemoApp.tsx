// THROWAWAY demo shell. Renders the REAL AppShell (BranchSwitcher/NavList/Topbar/UserMenu)
// around every club screen with a fake client, so the chrome matches production.
import { useMemo, useState } from 'react';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { AppShell } from '@/components/shell/AppShell';
import { BranchSwitcher } from '@/components/shell/BranchSwitcher';
import { visibleNav } from '@/club/nav';
import type { StaffSession } from '@/auth/staffTokenStore';
import type { OverviewState } from '@/club/overview/useOverview';
import { buildOverview } from '@/club/overview/overviewModel';
import { OverviewScreen } from '@/club/overview/OverviewScreen';
import { VenueScreen } from '@/club/venue/VenueScreen';
import { ClientsScreen } from '@/club/clients/ClientsScreen';
import { MonetizationScreen } from '@/club/monetization/MonetizationScreen';
import { ReportsScreen } from '@/club/reports/ReportsScreen';
import { JournalScreen } from '@/club/journal/JournalScreen';
import { BranchesScreen } from '@/club/branches/BranchesScreen';
import { InstallScreen } from '@/club/install/InstallScreen';
import { ProfileScreen } from '@/club/profile/ProfileScreen';
import { SettingsScreen } from '@/club/settings/SettingsScreen';
import type { MoneyPerms } from '@/club/clients/WalletPanel';
import { makeFakeClient } from './fakeClient';
import * as seed from './seed';

const BRANCH = 'demo-branch';
const ORG = 'demo-org';
const STAFF = 'demo-staff';

const branches = [
  { branchId: 'demo-branch', name: 'Центральный', city: 'Москва' },
  { branchId: 'demo-branch-2', name: 'Северный', city: 'Санкт-Петербург' }
];

const moneyPerms: MoneyPerms = { topUp: true, payDebt: true, correct: true, refund: true };

const demoSession: StaffSession = {
  staffUserId: STAFF,
  organizationId: ORG,
  displayName: 'Иван Владельцев',
  branchIds: [BRANCH, 'demo-branch-2'],
  permissions: [
    'club.overview.read', 'club.venue.manage', 'club.clients.create', 'club.billing.read',
    'club.billing.topup', 'club.billing.refund', 'club.monetization.manage', 'club.reports.read',
    'club.audit.read', 'club.settings.manage'
  ],
  accessToken: 'demo-access-token',
  accessTokenExpiresAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
  refreshToken: 'demo-refresh-token',
  refreshTokenExpiresAtUtc: new Date(Date.now() + 86_400_000).toISOString()
};

type ScreenKey =
  | 'overview' | 'venue' | 'clients' | 'monetization' | 'reports'
  | 'journal' | 'branches' | 'install' | 'profile' | 'settings';

// Real nav paths from src/club/nav.ts — drives NavList active state + navigation.
const PATH: Record<ScreenKey, string> = {
  overview: '/club', venue: '/club/venue', clients: '/club/clients', monetization: '/club/monetization',
  reports: '/club/reports', journal: '/club/journal', branches: '/club/branches', install: '/club/install',
  profile: '/club/profile', settings: '/club/settings'
};
const TITLE: Record<ScreenKey, string> = {
  overview: 'Обзор', venue: 'Зал и ПК', clients: 'Клиенты', monetization: 'Монетизация',
  reports: 'Отчёты', journal: 'Журнал', branches: 'Все филиалы', install: 'Установка',
  profile: 'Профиль', settings: 'Настройки'
};
const SCREEN_BY_PATH: Record<string, ScreenKey> = Object.fromEntries(
  (Object.keys(PATH) as ScreenKey[]).map(k => [PATH[k], k])
);

export function DemoApp() {
  const [screen, setScreen] = useState<ScreenKey>('overview');
  const client = useMemo(() => makeFakeClient(), []);

  const overviewState: OverviewState = useMemo(
    () => ({
      status: 'ready',
      data: buildOverview(seed.dashboardSummary, seed.devices, seed.pendingDevices),
      retry: () => {}
    }),
    []
  );

  function renderScreen() {
    switch (screen) {
      case 'overview':
        return <OverviewScreen state={overviewState} />;
      case 'venue':
        return <VenueScreen client={client as never} branchId={BRANCH} organizationId={ORG} canManageLayout />;
      case 'clients':
        return (
          <ClientsScreen client={client as never} branchId={BRANCH} organizationId={ORG}
            canCreate canViewBilling moneyPerms={moneyPerms} canPurchase />
        );
      case 'monetization':
        return (
          <MonetizationScreen client={client as never} branchId={BRANCH} organizationId={ORG}
            canManageTariffs canManageCatalog canManagePackages />
        );
      case 'reports':
        return <ReportsScreen client={client as never} branchId={BRANCH} />;
      case 'journal':
        return <JournalScreen client={client as never} branchId={BRANCH} />;
      case 'branches':
        return (
          <BranchesScreen client={client as never} branchIds={[BRANCH, 'demo-branch-2']}
            organizationId={ORG} onOpenBranch={() => {}} />
        );
      case 'install':
        return <InstallScreen client={client as never} canManage branches={branches} />;
      case 'profile':
        return <ProfileScreen session={demoSession} branches={branches} roleLabel="Владелец" onSignOut={() => {}} />;
      case 'settings':
        return <SettingsScreen client={client as never} branchId={BRANCH} organizationId={ORG} currentStaffUserId={STAFF} />;
    }
  }

  return (
    <ThemeProvider>
      <I18nProvider>
        <ToastProvider>
          <AppShell
            navGroups={visibleNav('owner')}
            sidebarHeader={
              <BranchSwitcher
                orgName="AFK4 Club"
                branches={branches}
                activeBranchId={BRANCH}
                onSelect={() => {}}
              />
            }
            activePath={PATH[screen]}
            subtitle={branches.find(b => b.branchId === BRANCH)?.name ?? ''}
            screenTitle={TITLE[screen]}
            userName="Иван Владельцев"
            roleLabel="Владелец"
            onNavigate={(path) => { const k = SCREEN_BY_PATH[path]; if (k !== undefined) setScreen(k); }}
            onSignOut={() => {}}
          >
            {renderScreen()}
          </AppShell>
        </ToastProvider>
      </I18nProvider>
    </ThemeProvider>
  );
}
