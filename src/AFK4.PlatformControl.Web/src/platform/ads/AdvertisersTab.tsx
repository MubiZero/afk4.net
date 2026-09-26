import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonTable } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdsApi } from '@/api/platformClients/ads';
import { AdvertiserFormDialog } from './AdvertiserFormDialog';
import {
  describeAdError,
  emptyAdvertiserForm,
  formFromAdvertiser,
  requestFromAdvertiserForm,
  type AdvertiserForm
} from './adsModel';
import { useLoadable } from '../useLoadable';

export type AdvertisersClient = Pick<AdsApi, 'listAdvertisers' | 'createAdvertiser' | 'updateAdvertiser'>;

interface Draft {
  advertiserId: string | null;
  form: AdvertiserForm;
}

export function AdvertisersTab({ client }: { client: AdvertisersClient }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useLoadable(async () => {
    const loaded = await client.listAdvertisers();
    return Array.isArray(loaded) ? loaded : [];
  });
  const [draft, setDraft] = useState<Draft | null>(null);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  function open(next: Draft) {
    setSaveError(null);
    setDraft(next);
  }

  function close() {
    if (pending) return;
    setDraft(null);
    setSaveError(null);
  }

  async function save() {
    if (draft === null || pending) return;
    setPending(true);
    setSaveError(null);
    try {
      const request = requestFromAdvertiserForm(draft.form);
      if (draft.advertiserId === null) {
        await client.createAdvertiser(request);
      } else {
        await client.updateAdvertiser(draft.advertiserId, request);
      }
      toast({ title: t(draft.advertiserId === null ? 'platform.ads.advertiser.created' : 'platform.ads.advertiser.saved'), variant: 'success' });
      setDraft(null);
      state.retry();
    } catch (cause) {
      // Форма остаётся открытой с тем, что человек ввёл: причина видна рядом с полями.
      setSaveError(describeAdError(cause, t));
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'error') {
    return <ErrorState title={t('platform.ads.advertisers.error.load')} message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;
  }
  if (state.status === 'loading') {
    return (
      <Loading>
        <SkeletonCard action>
          <p className="mgmt-drawer-hint">{t('platform.ads.advertisers.description')}</p>
          <SkeletonTable columns={4} />
        </SkeletonCard>
      </Loading>
    );
  }

  const advertisers = state.data;
  const createNew = () => open({ advertiserId: null, form: emptyAdvertiserForm() });

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.ads.advertisers.title')}</CardTitle>
        <Button onClick={createNew}>{t('platform.ads.advertisers.create')}</Button>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.ads.advertisers.description')}</p>

        {advertisers.length === 0 ? (
          <EmptyState message={t('platform.ads.advertisers.empty')} next={{ label: t('platform.ads.advertisers.createFirst'), onClick: createNew }} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.ads.advertisers.column.name')}</TableHead>
                <TableHead>{t('platform.ads.advertisers.column.contact')}</TableHead>
                <TableHead>{t('platform.ads.advertisers.column.createdAt')}</TableHead>
                <TableHead>{t('platform.ads.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {advertisers.map(advertiser => (
                <TableRow key={advertiser.advertiserId}>
                  <TableCell>{advertiser.name}</TableCell>
                  <TableCell className="pc-ad-contact">{advertiser.contact === '' ? '—' : advertiser.contact}</TableCell>
                  <TableCell>{formatDate(advertiser.createdAtUtc)}</TableCell>
                  <TableCell>
                    <span className="pc-cell-actions">
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={pending}
                        onClick={() => open({ advertiserId: advertiser.advertiserId, form: formFromAdvertiser(advertiser) })}
                      >
                        {t('platform.ads.edit')}
                      </Button>
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {draft === null ? null : (
          <AdvertiserFormDialog
            key={draft.advertiserId ?? 'new'}
            mode={draft.advertiserId === null ? 'create' : 'edit'}
            form={draft.form}
            pending={pending}
            error={saveError}
            onChange={form => setDraft({ ...draft, form })}
            onSubmit={() => void save()}
            onClose={close}
          />
        )}
      </CardContent>
    </Card>
  );
}
