import { useI18n } from '@afk4/i18n';
import type { WorkspaceId } from './operatorTypes';

export function SummarySidePanel({ workspace, currencyCode }: { workspace: WorkspaceId; currencyCode: string }) {
  const { t } = useI18n();

  const title = {
    map: t('op.summary.titlePc'),
    dashboard: t('op.summary.titleShift'),
    booking: t('op.summary.titleBooking'),
    pos: t('op.summary.titlePos'),
    shop_orders: t('op.shopOrders.title'),
    players: t('op.summary.titlePlayers'),
    payments: t('op.summary.titlePayments'),
    payment_cards: t('payments_cards.nav'),
    logs: t('op.summary.titleLogs'),
    settings: t('nav.settings'),
    review: t('op.summary.titleReview'),
    loyalty: t('op.loyalty.title')
  }[workspace];

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>{t('op.summary.detailsLabel')}</span>
          <h2>{title}</h2>
        </div>
        <span className="state-chip state-active">{t('op.summary.stateActive')}</span>
      </header>
      <section className="context-section">
        <div className="detail-row"><span>{t('reports.sum.revenue')}</span><strong>4 820 {currencyCode}</strong></div>
        <div className="detail-row"><span>{t('op.summary.inProgress')}</span><strong>{t('op.summary.actionsCount')}</strong></div>
        <div className="detail-row"><span>{t('reports.col.source')}</span><strong>{t('op.summary.localData')}</strong></div>
      </section>
      <button type="button" className="primary-wide">{t('op.summary.openAction')}</button>
    </aside>
  );
}
