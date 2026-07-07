import { useI18n } from '@afk4/i18n';
import { Package, Play, Search, UserRoundPlus, Users } from 'lucide-react';
import { initials, type PlayerClientItem } from '../operatorHelpers';
import { Skeleton, EmptyState, Money } from '../operatorPrimitives';
import {
  activePackageLabel,
  isNewClient,
  playerStatusLabel,
  relativeVisitLabel,
  type ClientLiveContext,
  type ClientSegment,
  type ClientSegmentId
} from './playersModel';

// Широкая таблица клиентов (замена master-списка ClientList — см. mock-v7): те же
// поиск/сегменты/skeleton/empty, что и в ClientList, плюс колонки Баланс/Долг/Сейчас-пакет/Визит.
export function ClientsTable({
  clients,
  segments,
  activeSegment,
  selectedClientId,
  search,
  showSkeleton,
  isLoading,
  emptyDescription,
  currencyCode,
  canCreatePlayer,
  liveContextByClient,
  nowMs,
  onNewClient,
  onSearchChange,
  onSelectSegment,
  onSelectClient
}: {
  clients: PlayerClientItem[];
  segments: ClientSegment[];
  activeSegment: ClientSegmentId;
  selectedClientId: string | null;
  search: string;
  showSkeleton: boolean;
  isLoading: boolean;
  emptyDescription: string;
  currencyCode: string;
  canCreatePlayer: boolean;
  liveContextByClient: Map<string, ClientLiveContext>;
  nowMs: number;
  onNewClient: () => void;
  onSearchChange: (value: string) => void;
  onSelectSegment: (id: ClientSegmentId) => void;
  onSelectClient: (playerAccountId: string | null) => void;
}) {
  const { t } = useI18n();

  return (
    <section className="table-panel">
      <div className="table-toolbar">
        <label className="tt-search">
          <Search size={14} aria-hidden="true" />
          <input
            placeholder={t('op.players.list.searchPlaceholder')}
            value={search}
            onChange={(event) => onSearchChange(event.currentTarget.value)}
          />
        </label>

        <div className="tt-segs" role="group" aria-label={t('op.players.segments.title')}>
          {segments.map((segment) => (
            <button
              key={segment.id}
              type="button"
              className={`ui-chip ui-chip--filter${activeSegment === segment.id ? ' is-active' : ''}`}
              onClick={() => onSelectSegment(segment.id)}
            >
              {segment.label}
              <b>{segment.count}</b>
            </button>
          ))}
        </div>

        <div className="tt-spacer" />

        {canCreatePlayer && (
          <button type="button" className="ui-btn ui-btn--primary" onClick={onNewClient}>
            <UserRoundPlus size={14} aria-hidden="true" />{t('op.players.newClient.openBtn')}
          </button>
        )}
      </div>

      <div className="ctable-head ctable-grid" aria-hidden="true">
        <span>{t('op.players.table.col.client')}</span>
        <span className="r">{t('op.players.table.col.balance')}</span>
        <span className="r">{t('op.players.table.col.debt')}</span>
        <span>{t('op.players.table.col.now')}</span>
        <span>{t('op.players.table.col.visit')}</span>
      </div>

      <div className="ctable-body">
        {showSkeleton ? (
          <div className="ctable-skeleton" aria-hidden="true">
            {Array.from({ length: 6 }).map((_, index) => (
              <Skeleton key={index} className="ctable-row-skel" />
            ))}
          </div>
        ) : clients.length > 0 ? (
          clients.map((client) => {
            const id = client.playerAccountId ?? null;
            const live = id ? liveContextByClient.get(id)?.session ?? null : null;
            const packageLabel = activePackageLabel(client.activePackageName, client.activePackageRemainingMinutes, t);
            const isDebt = client.debtMinorUnits > 0;
            const isInactive = client.status === 'inactive';
            return (
              <button
                key={id ?? client.name}
                type="button"
                className={`ctable-row ctable-grid${isDebt ? ' debt' : ''}${isInactive ? ' inactive' : ''}${id !== null && id === selectedClientId ? ' selected' : ''}`}
                onClick={() => onSelectClient(id)}
              >
                <span className="cc-client">
                  <span className="cc-av" aria-hidden="true">{initials(client.name)}</span>
                  <span className="cc-id">
                    <span className="cc-name">
                      <span className="nm">{client.name}</span>
                      {isNewClient(client.createdAtUtc, nowMs) && <span className="cc-tag">{t('op.players.tag.new')}</span>}
                      {isInactive && <span className="cc-tag">{playerStatusLabel(client.status, t)}</span>}
                    </span>
                    <span className="cc-phone">{client.phoneNumber || t('op.pos.cart.clientNoPhone')}</span>
                  </span>
                </span>
                <span className={`cc-num${client.balanceMinorUnits === 0 ? ' zero' : ''}`}>
                  <Money minorUnits={client.balanceMinorUnits} currencyCode={currencyCode} />
                </span>
                <span className={`cc-num${isDebt ? ' debt' : ' zero'}`}>
                  {isDebt ? <Money minorUnits={client.debtMinorUnits} currencyCode={currencyCode} /> : '—'}
                </span>
                <span className={`cc-now${live ? ' live' : packageLabel ? '' : ' none'}`}>
                  {live ? (
                    <>
                      <Play size={13} aria-hidden="true" />
                      <span className="txt">
                        {live.seatName} · {live.untilLabel ? t('op.players.context.until', { time: live.untilLabel }) : t('op.players.context.openTab')}
                      </span>
                    </>
                  ) : packageLabel ? (
                    <>
                      <Package size={13} aria-hidden="true" />
                      <span className="txt">{packageLabel}</span>
                    </>
                  ) : (
                    '—'
                  )}
                </span>
                <span className="cc-visit">{live ? t('op.players.visit.now') : relativeVisitLabel(client.lastActivityAtUtc, nowMs, t)}</span>
              </button>
            );
          })
        ) : isLoading ? null : (
          <EmptyState
            icon={<Users size={20} aria-hidden="true" />}
            title={t('op.players.list.emptyTitle')}
            description={emptyDescription}
          />
        )}
      </div>
    </section>
  );
}
