import { useI18n } from '@afk4/i18n';
import { Button, Inspector, Num } from '@afk4/ui/react';
import { Ban, TriangleAlert } from 'lucide-react';
import type { ReputationController } from './useReputation';

// Что сеть знает о человеке — двумя числами и без единого названия чужого клуба. Стоит в
// карточке (заявки и клиента), а не строкой в таблице: репутация — единственное чтение, которое
// пишется в аудит, и в списке это была бы запись про каждого, кого админ просто пролистал.
// Секция инспектора, а не своя карточка с кнопкой во всю ширину: это справка по запросу, и она
// не должна спорить за внимание с балансом и главной кнопкой.
export function ReputationCard({ controller }: { controller: ReputationController }) {
  const { t, formatDate } = useI18n();
  const { state, ask } = controller;

  return (
    <Inspector.Section label={t('op.reputation.title')}>
      {state.status === 'noPhone' && <p className="reputation-note">{t('op.reputation.noPhone')}</p>}

      {(state.status === 'idle' || state.status === 'loading') && (
        <div className="reputation-ask">
          <Button variant="ghost" size="sm" disabled={state.status === 'loading'} onClick={ask}>
            {state.status === 'loading' ? t('op.reputation.asking') : t('op.reputation.ask')}
          </Button>
          <p className="reputation-note">{t('op.reputation.auditNote')}</p>
        </div>
      )}

      {state.status === 'failed' && (
        <div className="reputation-ask">
          <Button variant="ghost" size="sm" onClick={ask}>{t('op.reputation.retry')}</Button>
          <p className="reputation-note reputation-note--failed" role="alert">{state.detail}</p>
        </div>
      )}

      {state.status === 'ready' && (
        <>
          {state.reputation.networkBanned && (
            <p className="reputation-ban" role="alert">
              <Ban size={14} aria-hidden="true" />
              <span>{t('op.reputation.banned')}</span>
            </p>
          )}

          {/* Ноль визитов — «сеть его не знает», а не повод насторожиться: подсвечены только
              неявки. */}
          <Inspector.Facts items={[
            { label: t('op.reputation.visits'), value: <Num>{state.reputation.networkVisits}</Num> },
            {
              label: t('op.reputation.noShows'),
              value: state.reputation.networkNoShows > 0
                ? <span className="reputation-attention"><TriangleAlert size={13} aria-hidden="true" /><Num>{state.reputation.networkNoShows}</Num></span>
                : <Num>{state.reputation.networkNoShows}</Num>
            },
          ]} />

          {/* «На когда посчитано» — не мелочь: сутки задержки и есть защита соседнего клуба от
              того, чтобы по свежести числа вычислили, когда человек у него играл. */}
          <p className="reputation-note">{t('op.reputation.asOf', { date: formatDate(state.reputation.calculatedAtUtc) })}</p>
          <p className="reputation-note">{t('op.reputation.privacyNote')}</p>
        </>
      )}
    </Inspector.Section>
  );
}
