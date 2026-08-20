import { useI18n } from '@afk4/i18n';
import { Ban, Network, TriangleAlert } from 'lucide-react';
import { reputationTone } from './reputationModel';
import type { ReputationController } from './useReputation';

// Что сеть знает о человеке — четырьмя числами и без единого названия чужого клуба. Стоит в
// карточке (заявки и клиента), а не строкой в таблице: репутация — единственное чтение, которое
// пишется в аудит, и в списке это была бы запись про каждого, кого админ просто пролистал.
export function ReputationCard({ controller }: { controller: ReputationController }) {
  const { t, formatDate } = useI18n();
  const { state, ask } = controller;

  return (
    <section className={`reputation-card${state.status === 'ready' ? ` is-${reputationTone(state.reputation)}` : ''}`}>
      <header className="reputation-head">
        <Network size={14} aria-hidden="true" />
        <span>{t('op.reputation.title')}</span>
      </header>

      {state.status === 'noPhone' && <p className="reputation-note">{t('op.reputation.noPhone')}</p>}

      {(state.status === 'idle' || state.status === 'loading') && (
        <>
          <button type="button" className="ui-btn ui-btn--block" disabled={state.status === 'loading'} onClick={ask}>
            {state.status === 'loading' ? t('op.reputation.asking') : t('op.reputation.ask')}
          </button>
          <p className="reputation-note">{t('op.reputation.auditNote')}</p>
        </>
      )}

      {state.status === 'failed' && (
        <>
          <p className="reputation-note reputation-note--failed" role="alert">{state.detail}</p>
          <button type="button" className="ui-btn ui-btn--block" onClick={ask}>{t('op.reputation.retry')}</button>
        </>
      )}

      {state.status === 'ready' && (
        <>
          {state.reputation.networkBanned && (
            <p className="reputation-ban" role="alert">
              <Ban size={14} aria-hidden="true" />
              <span>{t('op.reputation.banned')}</span>
            </p>
          )}

          <div className="reputation-numbers">
            <div>
              <span>{t('op.reputation.visits')}</span>
              <strong>{state.reputation.networkVisits}</strong>
            </div>
            <div className={state.reputation.networkNoShows > 0 ? 'is-attention' : undefined}>
              <span>{t('op.reputation.noShows')}</span>
              <strong>
                {state.reputation.networkNoShows > 0 && <TriangleAlert size={13} aria-hidden="true" />}
                {state.reputation.networkNoShows}
              </strong>
            </div>
          </div>

          {/* «На когда посчитано» — не мелочь: сутки задержки и есть защита соседнего клуба от
              того, чтобы по свежести числа вычислили, когда человек у него играл. */}
          <p className="reputation-note">{t('op.reputation.asOf', { date: formatDate(state.reputation.calculatedAtUtc) })}</p>
          <p className="reputation-note">{t('op.reputation.privacyNote')}</p>
        </>
      )}
    </section>
  );
}
