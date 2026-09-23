import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { MgmtTable } from '../management/kit/MgmtTable';
import { downloadTextFile, formatMinorUnits } from '../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { PartialLoadFailure } from '../operatorPrimitives';
import type { FloorMapDto } from '../operatorApiClients';
import type { GameplayTimeReportResultDto } from '../api/clients/shifts';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportInstantQuery, type ReportDateRange } from './reportRange';
import { createDetailReportClients } from './reportClient';

/**
 * Куда ушло игровое время и что оно принесло.
 *
 * Отчёт можно было заказать себе на почту — в форме рассылок он есть с самого начала, — но
 * открыть его в кабинете или скачать по требованию было негде: сервер отдавал и данные, и CSV,
 * а вкладки не существовало.
 */
export function GameplayTimeReport({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate, formatNumber } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<GameplayTimeReportResultDto | null>(null);
  // Место сессия называет только идентификатором. Показывать его человеку бессмысленно: имя
  // места лежит в плане зала, и один запрос за ним дешевле, чем отчёт, по которому не понять,
  // какой ПК столько наиграл.
  const [seatNames, setSeatNames] = useState<Record<string, string>>({});
  // План зала нужен отчёту только ради имён мест: его отказ не прячет отчёт, но и не молчит —
  // колонка из одних прочерков без объяснения читается как «ПК не было».
  const [seatNamesError, setSeatNamesError] = useState<OperatorErrorProjection | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState<OperatorErrorProjection | undefined>();

  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(projectOperatorError(t('op.reports.backendRequired'), t)); return; }
    setState('loading');
    setSeatNamesError(null);
    const clients = createDetailReportClients(backend);
    const [report, floorMap] = await Promise.allSettled([
      clients.shifts.getGameplayTimeReport(backend.branchId, toReportInstantQuery(range)),
      clients.floorMap.getFloorMap(backend.branchId)
    ]);
    if (floorMap.status === 'fulfilled') setSeatNames(seatNamesOf(floorMap.value));
    else setSeatNamesError(projectOperatorError(floorMap.reason, t));
    if (report.status === 'fulfilled') { setData(report.value); setState('ready'); }
    else { setError(projectOperatorError(report.reason, t)); setState('error'); }
  }, [backend, range, t]);
  useEffect(() => { void load(); }, [load]);

  async function retrySeatNames() {
    if (!backend) return;
    setSeatNamesError(null);
    try {
      setSeatNames(seatNamesOf(await createDetailReportClients(backend).floorMap.getFloorMap(backend.branchId)));
    } catch (reason) { setSeatNamesError(projectOperatorError(reason, t)); }
  }

  async function exportCsv() {
    if (!backend) return;
    const clients = createDetailReportClients(backend);
    const csv = await clients.shifts.exportGameplayTimeReportCsv(backend.branchId, toReportInstantQuery(range));
    downloadTextFile(`gameplay-time-${range.from}-${range.to}.csv`, csv);
  }

  const hours = (seconds: number) => formatNumber(Math.round(seconds / 360) / 10);

  return (
    <ManagementScreen
      title={t('op.reports.gameplay.title')}
      subtitle={t('op.reports.gameplay.subtitle')}
      contentWidth="full"
      state={state}
      failure={error}
      onRetry={() => void load()}
    >
      <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} onExport={() => void exportCsv()} />
      {data ? (
        <>
          {seatNamesError !== null && (
            <PartialLoadFailure text={t('op.reports.gameplay.seatNamesFailed', { reason: seatNamesError.detail })} failure={seatNamesError} onRetry={() => void retrySeatNames()} />
          )}
          <dl className="reports-figures">
            <div><dt>{t('op.reports.gameplay.total')}</dt><dd>{hours(data.totalDurationSeconds)}</dd></div>
            <div><dt>{t('op.reports.gameplay.fromPackages')}</dt><dd>{hours(data.totalPackageSeconds)}</dd></div>
            <div><dt>{t('op.reports.gameplay.bonus')}</dt><dd>{hours(data.totalBonusSeconds)}</dd></div>
            <div>
              <dt>{t('op.reports.gameplay.revenue')}</dt>
              <dd>{formatMinorUnits(data.gameplayRevenueTotal.minorUnits, data.gameplayRevenueTotal.currencyCode)}</dd>
            </div>
          </dl>
          <MgmtTable<GameplayTimeReportResultDto['rows'][number]>
            columns={[
              { key: 'seat', header: t('op.reports.gameplay.col.seat'), render: (row) => seatNames[row.seatId] ?? '—' },
              { key: 'player', header: t('op.reports.gameplay.col.player'), render: (row) => t(playerKindKey(row.playerKind)) },
              { key: 'started', header: t('op.reports.gameplay.col.started'), render: (row) => row.startedAtUtc === null ? '—' : formatDate(row.startedAtUtc) },
              { key: 'duration', header: t('op.reports.gameplay.col.duration'), align: 'end', render: (row) => hours(row.durationSeconds) },
              {
                key: 'revenue',
                header: t('op.reports.gameplay.col.revenue'),
                align: 'end',
                render: (row) => formatMinorUnits(row.gameplayRevenue.minorUnits, row.gameplayRevenue.currencyCode)
              }
            ]}
            rows={data.rows}
            rowKey={(row) => row.sessionId}
            gridTemplate="minmax(120px, 1fr) minmax(140px, 1fr) minmax(140px, 1fr) 120px 140px"
            empty={{ title: t('op.reports.empty'), next: { kind: 'elsewhere', hint: t('op.reports.emptyHint') } }}
          />
          {/* Сервер отдаёт не больше limit строк. Молчать об этом нельзя: неполный отчёт,
              который выглядит полным, — это неверные выводы о загрузке зала. */}
          {data.rows.length >= data.limit
            ? <p className="mgmt-drawer-hint">{t('op.reports.truncated', { count: data.limit })}</p>
            : null}
        </>
      ) : null}
    </ManagementScreen>
  );
}

function seatNamesOf(floorMap: FloorMapDto): Record<string, string> {
  return Object.fromEntries((floorMap.seats ?? []).map((seat) => [seat.seatId, seat.seatName]));
}

function playerKindKey(kind: string): MessageKey {
  return kind === 'guest' ? 'op.reports.gameplay.player.guest' : 'op.reports.gameplay.player.member';
}
