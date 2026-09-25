import { useState, type ReactNode } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonTable } from '@/components/ui/skeletons';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdsApi } from '@/api/platformClients/ads';
import type { AdImpressionRowDto } from '@/api/types';
import { defaultReportRange, reportTotals, validateReportRange } from './adsModel';
import { useLoadable } from '../useLoadable';

export type ReportClient = Pick<AdsApi, 'report' | 'listCampaigns'>;

interface Filters {
  from: string;
  to: string;
  /** Пусто — все кампании. */
  campaignId: string;
}

export function ReportTab({ client, now = new Date() }: {
  client: ReportClient;
  /** Точка отсчёта «последних 30 дней» — тесту нужна неподвижная. */
  now?: Date;
}) {
  const { t, formatNumber } = useI18n();
  const [draft, setDraft] = useState<Filters>(() => ({ ...defaultReportRange(now), campaignId: '' }));
  const [applied, setApplied] = useState<Filters>(draft);
  const [rangeError, setRangeError] = useState<string | null>(null);
  // Кампании — только ради фильтра: без них отчёт всё равно строится по всем.
  const campaigns = useLoadable(async () => {
    const loaded = await client.listCampaigns();
    return Array.isArray(loaded) ? loaded : [];
  });
  const report = useLoadable(async () => {
    const loaded = await client.report({ from: applied.from, to: applied.to, campaignId: applied.campaignId === '' ? null : applied.campaignId });
    return Array.isArray(loaded) ? loaded : [];
  }, [applied.from, applied.to, applied.campaignId]);

  function apply(next: Filters) {
    const error = validateReportRange(next.from, next.to);
    if (error !== null) {
      setRangeError(t(error.key, error.values));
      return;
    }
    setRangeError(null);
    // Те же условия — человек хочет свежие цифры: показы с ПК приходят раз в час.
    if (next.from === applied.from && next.to === applied.to && next.campaignId === applied.campaignId) report.retry();
    else setApplied(next);
  }

  function resetCampaign() {
    const next = { ...applied, campaignId: '' };
    setDraft(next);
    apply(next);
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.ads.report.title')}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.ads.report.description')}</p>

        <form className="pc-filters" onSubmit={event => { event.preventDefault(); apply(draft); }}>
          <Filter label={t('platform.ads.report.from')}>
            <Input type="date" value={draft.from} onChange={event => setDraft({ ...draft, from: event.target.value })} />
          </Filter>
          <Filter label={t('platform.ads.report.to')}>
            <Input type="date" value={draft.to} onChange={event => setDraft({ ...draft, to: event.target.value })} />
          </Filter>
          <Filter label={t('platform.ads.report.campaign')}>
            <Select value={draft.campaignId} onChange={event => setDraft({ ...draft, campaignId: event.target.value })}>
              <option value="">{t('platform.ads.report.allCampaigns')}</option>
              {(campaigns.status === 'ready' ? campaigns.data : []).map(campaign => (
                <option key={campaign.campaignId} value={campaign.campaignId}>{campaign.name}</option>
              ))}
            </Select>
          </Filter>
          <div><Button type="submit">{t('platform.ads.report.apply')}</Button></div>
        </form>
        {rangeError !== null ? <p className="pc-error-text" role="alert">{rangeError}</p> : null}

        {report.status === 'error' ? (
          <ErrorState title={t('platform.ads.report.error.load')} message={report.message} retryLabel={report.canRetry ? t('state.retry') : undefined} onRetry={report.canRetry ? report.retry : undefined} />
        ) : report.status === 'loading' ? (
          <Loading><SkeletonTable columns={8} rows={6} /></Loading>
        ) : report.data.length === 0 ? (
          applied.campaignId !== '' ? (
            <EmptyState message={t('platform.ads.report.emptyFiltered')} next={{ label: t('state.empty.resetFilter'), onClick: resetCampaign }} />
          ) : (
            <EmptyState message={t('platform.ads.report.empty')} next="calm" />
          )
        ) : (
          <ReportTable rows={report.data} formatNumber={formatNumber} />
        )}
      </CardContent>
    </Card>
  );
}

function ReportTable({ rows, formatNumber }: {
  rows: readonly AdImpressionRowDto[];
  formatNumber: (value: number) => string;
}) {
  const { t } = useI18n();
  const totals = reportTotals(rows);
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('platform.ads.report.column.day')}</TableHead>
          <TableHead>{t('platform.ads.report.column.campaign')}</TableHead>
          <TableHead>{t('platform.ads.report.column.creative')}</TableHead>
          <TableHead>{t('platform.ads.report.column.club')}</TableHead>
          <TableHead>{t('platform.ads.report.column.branch')}</TableHead>
          <TableHead>{t('platform.ads.report.column.city')}</TableHead>
          <TableHead className="pc-num">{t('platform.ads.report.column.impressions')}</TableHead>
          <TableHead className="pc-num">{t('platform.ads.report.column.seconds')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={`${row.day}:${row.creativeId}:${row.branchId}`}>
            <TableCell className="pc-ad-day">{row.day}</TableCell>
            <TableCell>{row.campaignName}</TableCell>
            <TableCell>{row.creativeTitle}</TableCell>
            <TableCell>{row.organizationName === '' ? '—' : row.organizationName}</TableCell>
            <TableCell>{row.branchName === '' ? '—' : row.branchName}</TableCell>
            <TableCell>{row.city === '' ? '—' : row.city}</TableCell>
            <TableCell className="pc-num">{formatNumber(row.impressions)}</TableCell>
            <TableCell className="pc-num">{formatNumber(row.shownSeconds)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
      <tfoot className="pc-ad-totals">
        <tr>
          <td colSpan={6}>{t('platform.ads.report.total')}</td>
          <td className="pc-num">{formatNumber(totals.impressions)}</td>
          <td className="pc-num">{formatNumber(totals.shownSeconds)}</td>
        </tr>
      </tfoot>
    </Table>
  );
}

function Filter({ label, children }: { label: string; children: ReactNode }) {
  return <label><span>{label}</span>{children}</label>;
}
