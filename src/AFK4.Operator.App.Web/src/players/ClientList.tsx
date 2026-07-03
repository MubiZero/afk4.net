import { useI18n } from '@afk4/i18n';
import { Search, UserRoundPlus, Users } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import { Skeleton, EmptyState, Money } from '../operatorPrimitives';
import { playerStatusLabel, type ClientSegment, type ClientSegmentId } from './playersModel';

// Master-список клиентов: поиск + сегмент-чипы (стабильные id) + строки + skeleton/empty.
export function ClientList({
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
  onNewClient: () => void;
  onSearchChange: (value: string) => void;
  onSelectSegment: (id: ClientSegmentId) => void;
  onSelectClient: (playerAccountId: string | null) => void;
}) {
  const { t } = useI18n();

  return (
    <section className="clients-panel clients-list-panel">
      <header className="clients-panel-title">
        <span className="clients-panel-title-text">{t('op.players.list.title')}</span>
        {canCreatePlayer && (
          <button type="button" className="clients-new-client-btn" onClick={onNewClient}>
            <UserRoundPlus size={15} aria-hidden="true" />{t('op.players.newClient.openBtn')}
          </button>
        )}
      </header>

      <label className="clients-search">
        <Search size={14} aria-hidden="true" />
        <input
          placeholder={t('op.players.list.searchPlaceholder')}
          value={search}
          onChange={(event) => onSearchChange(event.currentTarget.value)}
        />
      </label>

      <div className="clients-segment-chips" role="group" aria-label={t('op.players.segments.title')}>
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

      <div className="clients-list">
        {showSkeleton ? (
          <div className="clients-list-skeleton" aria-hidden="true">
            {Array.from({ length: 5 }).map((_, index) => (
              <Skeleton key={index} className="client-row-skel" />
            ))}
          </div>
        ) : clients.length > 0 ? (
          clients.map((client) => (
            <button
              key={client.playerAccountId ?? client.name}
              type="button"
              className={`ui-card ui-card--interactive ui-card--edge client-row ${client.tone}${client.status === 'inactive' ? ' is-inactive' : ''}${client.playerAccountId === selectedClientId ? ' selected' : ''}`}
              onClick={() => onSelectClient(client.playerAccountId ?? null)}
            >
              <div className="client-row-info">
                <strong className="client-row-name">
                  <span className="client-row-name-text">{client.name}</span>
                  {client.status === 'inactive' && (
                    <span className="ui-chip ui-chip--status ui-chip--xs is-neutral">{playerStatusLabel(client.status, t)}</span>
                  )}
                  {client.debtMinorUnits > 0 && (
                    <span className="ui-chip ui-chip--status ui-chip--xs is-danger">
                      {t('op.players.chip.debt')} <Money minorUnits={client.debtMinorUnits} currencyCode={currencyCode} />
                    </span>
                  )}
                </strong>
                <em className="client-row-detail">{client.detail}</em>
              </div>
              <div className="client-row-figures">
                <Money minorUnits={client.balanceMinorUnits} currencyCode={currencyCode} />
              </div>
            </button>
          ))
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
