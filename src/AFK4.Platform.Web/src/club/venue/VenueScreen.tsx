// src/club/venue/VenueScreen.tsx
import { useState } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { ClubApiClient } from '@/api/clubApi';
import { useDevices } from './useDevices';
import { DevicesTable } from './DevicesTable';
import { DeviceDrawer } from './DeviceDrawer';
import type { DeviceRow } from './devicesModel';

export function VenueScreen({ client, branchId }: { client: ClubApiClient; branchId: string }) {
  const { t } = useI18n();
  const state = useDevices(client, branchId);
  const [selected, setSelected] = useState<DeviceRow | null>(null);

  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const { active, pending, seatOptions } = state.data;
  return (
    <>
      <Tabs defaultValue="devices">
        <TabsList>
          <TabsTrigger value="devices">{t('venue.tab.devices')}</TabsTrigger>
          {pending.length > 0 && <TabsTrigger value="pending">{`${t('venue.tab.pending')} (${pending.length})`}</TabsTrigger>}
          <TabsTrigger value="map">{t('venue.tab.map')}</TabsTrigger>
        </TabsList>
        <TabsContent value="devices">
          <DevicesTable rows={active} emptyMessage={t('devices.empty.active')} onSelect={setSelected} />
        </TabsContent>
        {pending.length > 0 && (
          <TabsContent value="pending">
            <DevicesTable rows={pending} emptyMessage={t('devices.empty.pending')} onSelect={setSelected} />
          </TabsContent>
        )}
        <TabsContent value="map">
          <p className="text-sm text-muted-foreground">{t('venue.map.soon')}</p>
        </TabsContent>
      </Tabs>

      <Sheet open={selected !== null} onOpenChange={open => { if (!open) setSelected(null); }}>
        <SheetContent closeLabel={t('common.close')}>
          {selected && (
            <>
              <SheetTitle>{selected.displayName}</SheetTitle>
              <DeviceDrawer
                device={selected}
                seatOptions={seatOptions}
                client={client}
                onDone={() => { setSelected(null); state.retry(); }}
              />
            </>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}
