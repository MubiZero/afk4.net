import { useI18n } from '@afk4/i18n';
import { Search, Users } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import { formatMinorUnits } from '../operatorHelpers';
import { Skeleton, EmptyState } from '../operatorPrimitives';
import { playerStatusLabel, type ClientSegment, type ClientSegmentId } from './playersModel';

// Master-список клиентов: поиск + сегмент-чипы (стабильные id) + строки + skeleton/empty.
export function ClientList({
  clients,
  segments,
  activeSegment,
  selectedClientId,
  search,
  showSkeleton,
  emptyDescription,
  currencyCode,
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
  emptyDescription: string;
  currencyCode: string;
  onSearchChange: (value: string) => void;
  onSelectSegment: (id: ClientSegmentId) => void;
  onSelectClient: (playerAccountId: string | null) => void;
}) {
  const { t } = useI18n();

  return (
    <section className="clients-panel clients-list-panel">
      <header className="clients-panel-title">
        <span>{t('op.players.list.title')}</span>
        <strong>{t('op.players.list.subtitle')}</strong>
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
            className={`clients-segment-chip${activeSegment === segment.id ? ' active' : ''}`}
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
        ) : clients.length === 0 ? (
          <EmptyState
            icon={<Users size={20} aria-hidden="true" />}
            title={t('op.players.list.emptyTitle')}
            description={emptyDescription}
          />
        ) : (
          clients.map((client) => (
            <button
              key={client.playerAccountId ?? client.name}
              type="button"
              className={`client-row ${client.tone}${client.status === 'inactive' ? ' is-inactive' : ''}${client.playerAccountId === selectedClientId ? ' selected' : ''}`}
              onClick={() => onSelectClient(client.playerAccountId ?? null)}
            >
              <span className="client-row-status" aria-hidden="true">{playerStatusLabel(client.status, t)}</span>
              <div className="client-row-info">
                <strong className="client-row-name">{client.name}</strong>
                <em className="client-row-detail">{client.detail}</em>
              </div>
              <b className="client-row-balance">{formatMinorUnits(client.balanceMinorUnits, currencyCode)}</b>
              {client.debtMinorUnits > 0 && (
                <small className="client-row-debt">{formatMinorUnits(client.debtMinorUnits, currencyCode)}</small>
              )}
            </button>
          ))
        )}
      </div>
    </section>
  );
}
