import { useState } from 'react';
import type { AdComplaintReasonName } from '@afk4/contracts';
import type { MessageKey } from '@afk4/i18n';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field } from '@/components/ui/field';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonTable } from '@/components/ui/skeletons';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdsApi } from '@/api/platformClients/ads';
import type { AdComplaintDto } from '@/api/types';
import { useLoadable } from '../useLoadable';

export type ComplaintsClient = Pick<AdsApi, 'listComplaints' | 'resolveComplaint'>;

// Зеркало AdComplaintLimits.ResolutionMax: пределы кодогенерация в TS не переносит.
const RESOLUTION_MAX = 500;

const REASON_KEY: Record<AdComplaintReasonName, MessageKey> = {
  banned_goods: 'platform.ads.complaints.reason.banned_goods',
  minors: 'platform.ads.complaints.reason.minors',
  misleading: 'platform.ads.complaints.reason.misleading',
  other_club: 'platform.ads.complaints.reason.other_club',
  other: 'platform.ads.complaints.reason.other'
};

/**
 * Жалобы клубов на рекламу на их ПК (спека рекламы, §8.4). Клуб — распространитель, но снять
 * рекламу сам не может: решает платформа. Снять креатив — на странице кампании, здесь — ответ.
 */
export function ComplaintsTab({ client, onOpenCampaign }: {
  client: ComplaintsClient;
  onOpenCampaign: (campaignId: string) => void;
}) {
  const { t, formatDate } = useI18n();
  const [showClosed, setShowClosed] = useState(false);
  const [resolving, setResolving] = useState<AdComplaintDto | null>(null);
  const complaints = useLoadable(async () => {
    const loaded = await client.listComplaints(!showClosed);
    return Array.isArray(loaded) ? loaded : [];
  }, [showClosed]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.ads.complaints.title')}</CardTitle>
        <label className="pc-check-row">
          <Switch checked={showClosed} onCheckedChange={setShowClosed} />
          {t('platform.ads.complaints.showClosed')}
        </label>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.ads.complaints.description')}</p>
        {complaints.status === 'error' ? (
          <ErrorState title={t('platform.ads.complaints.error.load')} message={complaints.message} retryLabel={complaints.canRetry ? t('state.retry') : undefined} onRetry={complaints.canRetry ? complaints.retry : undefined} />
        ) : complaints.status === 'loading' ? (
          <Loading><SkeletonTable columns={6} rows={4} /></Loading>
        ) : complaints.data.length === 0 ? (
          <EmptyState message={t(showClosed ? 'platform.ads.complaints.emptyAll' : 'platform.ads.complaints.empty')} next="calm" />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.ads.complaints.column.date')}</TableHead>
                <TableHead>{t('platform.ads.complaints.column.club')}</TableHead>
                <TableHead>{t('platform.ads.complaints.column.ad')}</TableHead>
                <TableHead>{t('platform.ads.complaints.column.reason')}</TableHead>
                <TableHead>{t('platform.ads.complaints.column.status')}</TableHead>
                <TableHead>{t('platform.ads.complaints.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {complaints.data.map(complaint => (
                <TableRow key={complaint.complaintId}>
                  <TableCell>{formatDate(complaint.createdAtUtc)}</TableCell>
                  <TableCell>
                    <span className="font-medium">{complaint.organizationName}</span>
                    {complaint.reportedBy ? <span className="mgmt-drawer-hint"> · {complaint.reportedBy}</span> : null}
                  </TableCell>
                  <TableCell>
                    <span lang="tg">{complaint.creativeTitle}</span>
                    <span className="mgmt-drawer-hint"> · {complaint.advertiser}</span>
                    {complaint.creativeArchived ? <> <Badge variant="outline">{t('platform.ads.complaints.archived')}</Badge></> : null}
                  </TableCell>
                  <TableCell>
                    {t(REASON_KEY[complaint.reason as AdComplaintReasonName] ?? 'platform.ads.complaints.reason.other')}
                    {complaint.comment ? <p className="mgmt-drawer-hint">{complaint.comment}</p> : null}
                  </TableCell>
                  <TableCell>
                    {complaint.resolvedAtUtc
                      ? <><Badge variant="success">{t('platform.ads.complaints.closed')}</Badge>{complaint.resolution ? <p className="mgmt-drawer-hint">{complaint.resolution}</p> : null}</>
                      : <Badge variant="warning">{t('platform.ads.complaints.open')}</Badge>}
                  </TableCell>
                  <TableCell className="pc-cell-actions">
                    <Button variant="outline" onClick={() => onOpenCampaign(complaint.campaignId)}>{t('platform.ads.complaints.openCampaign')}</Button>
                    {complaint.resolvedAtUtc ? null : <Button onClick={() => setResolving(complaint)}>{t('platform.ads.complaints.resolve')}</Button>}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      {resolving ? (
        <ResolveDialog
          complaint={resolving}
          client={client}
          onDone={() => { setResolving(null); complaints.retry(); }}
          onClose={() => setResolving(null)}
        />
      ) : null}
    </Card>
  );
}

function ResolveDialog({ complaint, client, onDone, onClose }: {
  complaint: AdComplaintDto;
  client: ComplaintsClient;
  onDone: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [resolution, setResolution] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const trimmed = resolution.trim();
  const tooLong = trimmed.length > RESOLUTION_MAX;

  async function submit() {
    setPending(true);
    setError(null);
    try {
      await client.resolveComplaint(complaint.complaintId, trimmed);
      onDone();
    } catch (cause) {
      setError(describeApiError(cause, t));
      setPending(false);
    }
  }

  return (
    <Dialog
      open
      title={t('platform.ads.complaints.resolveTitle')}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
          <Button disabled={pending || trimmed === '' || tooLong} onClick={() => void submit()}>{t('platform.ads.complaints.resolve')}</Button>
        </>
      }
    >
      <div className="pc-panel-body">
        <ErrorBanner message={error} dismissLabel={t('common.close')} />
        <p className="mgmt-drawer-hint">
          {complaint.creativeArchived ? t('platform.ads.complaints.resolveHintArchived') : t('platform.ads.complaints.resolveHint')}
        </p>
        <Field label={t('platform.ads.complaints.resolution')} htmlFor="complaint-resolution"
          error={tooLong ? t('platform.ads.complaints.resolutionTooLong', { max: RESOLUTION_MAX }) : undefined}>
          <Textarea id="complaint-resolution" rows={3} value={resolution} onChange={event => setResolution(event.target.value)} />
        </Field>
      </div>
    </Dialog>
  );
}
