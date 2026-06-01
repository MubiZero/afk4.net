// src/club/settings/SettingsScreen.tsx
import { useState } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useSettings } from './useSettings';
import { BranchProfileForm } from './BranchProfileForm';
import { OperatorsTable } from './OperatorsTable';
import { OperatorDrawer } from './OperatorDrawer';
import { CreateOperatorDialog } from './CreateOperatorDialog';
import type { OperatorRow } from './settingsModel';

export function SettingsScreen({ client, branchId, organizationId, currentStaffUserId }: {
  client: ClubApiClient;
  branchId: string;
  organizationId: string;
  currentStaffUserId: string;
}) {
  const { t } = useI18n();
  const state = useSettings(client, branchId);
  const [selected, setSelected] = useState<OperatorRow | null>(null);
  const [creating, setCreating] = useState(false);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { profile, requireManualDeviceApproval, preferredLocale, operators } = state.data;
  return (
    <>
      <Tabs defaultValue="branch">
        <TabsList>
          <TabsTrigger value="branch">{t('settings.tab.branch')}</TabsTrigger>
          <TabsTrigger value="operators">{t('settings.tab.operators')}</TabsTrigger>
        </TabsList>
        <TabsContent value="branch">
          <BranchProfileForm
            profile={profile}
            requireManualDeviceApproval={requireManualDeviceApproval}
            preferredLocale={preferredLocale}
            branchId={branchId}
            client={client}
            onDone={state.retry}
          />
        </TabsContent>
        <TabsContent value="operators">
          <div className="mb-3 flex justify-end">
            <Button onClick={() => setCreating(true)}>{t('operators.create.title')}</Button>
          </div>
          <OperatorsTable rows={operators} emptyMessage={t('operators.empty')} onSelect={setSelected} />
        </TabsContent>
      </Tabs>

      <Sheet open={selected !== null} onOpenChange={open => { if (!open) setSelected(null); }}>
        <SheetContent closeLabel={t('common.close')}>
          {selected && (
            <>
              <SheetTitle>{selected.displayName}</SheetTitle>
              <OperatorDrawer
                operator={selected}
                branchId={branchId}
                currentStaffUserId={currentStaffUserId}
                client={client}
                onDone={() => { setSelected(null); state.retry(); }}
              />
            </>
          )}
        </SheetContent>
      </Sheet>

      <CreateOperatorDialog
        open={creating}
        branchId={branchId}
        organizationId={organizationId}
        client={client}
        onOpenChange={setCreating}
        onDone={state.retry}
      />
    </>
  );
}
