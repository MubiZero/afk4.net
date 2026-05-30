import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { useReport } from './useReport';
import { ExportButton } from './ExportButton';
import type { ReportView, ReportFormatters } from './reportsModel';

export function ReportTab<T>({ load, build, deps, onExport, filename }: {
  load: () => Promise<T>;
  build: (data: T, fmt: ReportFormatters) => ReportView;
  deps: readonly unknown[];
  onExport: () => Promise<Blob>;
  filename: string;
}) {
  const { t, formatCurrency, formatNumber, formatDate } = useI18n();
  const state = useReport(load, deps);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const view = build(state.data, { formatCurrency, formatNumber, formatDate });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end">
        <ExportButton onExport={onExport} filename={filename} />
      </div>

      {view.summaryCards.length > 0 && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {view.summaryCards.map(card => (
            <Card key={card.labelKey}>
              <CardContent className="p-4">
                <div className="text-xs text-muted-foreground">{t(card.labelKey)}</div>
                <div className="mt-1 text-lg font-semibold tabular-nums">{card.value}</div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {view.rows.length === 0 ? (
        <EmptyState message={t('reports.empty')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              {view.columns.map(col => <TableHead key={col.key}>{t(col.labelKey)}</TableHead>)}
            </TableRow>
          </TableHeader>
          <TableBody>
            {view.rows.map((row, index) => (
              <TableRow key={index}>
                {view.columns.map(col => <TableCell key={col.key} className="tabular-nums">{row[col.key]}</TableCell>)}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <p className="text-xs text-muted-foreground">{t('reports.limitNote')}</p>
    </div>
  );
}
