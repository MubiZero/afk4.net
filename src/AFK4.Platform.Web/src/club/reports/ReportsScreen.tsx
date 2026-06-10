import { useState } from 'react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { ReportsApi } from '@/api/clients/reports';
import { DateRangeControl } from './DateRangeControl';
import { ReportTab } from './ReportTab';
import {
  presetRange, isoToDateInput, type DateRange,
  buildShiftReport, buildSalesReport, buildGameplayReport, buildCashReport, buildOperatorActionReport
} from './reportsModel';

type Client = Pick<ReportsApi,
  'getShiftReport' | 'getSalesReport' | 'getGameplayTimeReport' | 'getCashOperationReport' | 'getOperatorActionReport' | 'fetchReportCsv'>;

export function ReportsScreen({ client, branchId }: { client: Client; branchId: string }) {
  const { t } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const deps = [branchId, range.fromUtc, range.toUtc] as const;
  const suffix = isoToDateInput(range.fromUtc);

  return (
    <div className="flex flex-col gap-4">
      <DateRangeControl value={range} onChange={setRange} />
      <Tabs defaultValue="shifts">
        <TabsList>
          <TabsTrigger value="shifts">{t('reports.tab.shifts')}</TabsTrigger>
          <TabsTrigger value="sales">{t('reports.tab.sales')}</TabsTrigger>
          <TabsTrigger value="gameplay">{t('reports.tab.gameplay')}</TabsTrigger>
          <TabsTrigger value="cash">{t('reports.tab.cash')}</TabsTrigger>
          <TabsTrigger value="operatorActions">{t('reports.tab.operatorActions')}</TabsTrigger>
        </TabsList>
        <TabsContent value="shifts">
          <ReportTab
            load={() => client.getShiftReport(branchId, range.fromUtc, range.toUtc)}
            build={buildShiftReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'shifts', range.fromUtc, range.toUtc)}
            filename={`shifts-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="sales">
          <ReportTab
            load={() => client.getSalesReport(branchId, range.fromUtc, range.toUtc)}
            build={buildSalesReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'sales', range.fromUtc, range.toUtc)}
            filename={`sales-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="gameplay">
          <ReportTab
            load={() => client.getGameplayTimeReport(branchId, range.fromUtc, range.toUtc)}
            build={buildGameplayReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'gameplay-time', range.fromUtc, range.toUtc)}
            filename={`gameplay-time-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="cash">
          <ReportTab
            load={() => client.getCashOperationReport(branchId, range.fromUtc, range.toUtc)}
            build={buildCashReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'cash-operations', range.fromUtc, range.toUtc)}
            filename={`cash-operations-${suffix}.csv`} />
        </TabsContent>
        <TabsContent value="operatorActions">
          <ReportTab
            load={() => client.getOperatorActionReport(branchId, range.fromUtc, range.toUtc)}
            build={buildOperatorActionReport} deps={deps}
            onExport={() => client.fetchReportCsv(branchId, 'operator-actions', range.fromUtc, range.toUtc)}
            filename={`operator-actions-${suffix}.csv`} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
