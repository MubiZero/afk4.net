import type { WorkspaceId } from './operatorTypes';

export function SummarySidePanel({ workspace, currencyCode }: { workspace: WorkspaceId; currencyCode: string }) {
  const title = {
    map: 'PC-01',
    dashboard: 'Смена',
    booking: 'Бронь 16:00',
    pos: 'Корзина',
    players: 'Amir K.',
    payments: 'Платеж 14:30',
    payment_cards: 'Приём платежей',
    logs: 'Событие журнала',
    settings: 'Настройки',
    review: 'Проверка'
  }[workspace];

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>Детали</span>
          <h2>{title}</h2>
        </div>
        <span className="state-chip state-active">Активно</span>
      </header>
      <section className="context-section">
        <div className="detail-row"><span>Выручка</span><strong>4 820 {currencyCode}</strong></div>
        <div className="detail-row"><span>В работе</span><strong>2 действия</strong></div>
        <div className="detail-row"><span>Источник</span><strong>Локальные данные</strong></div>
      </section>
      <button type="button" className="primary-wide">Открыть действие</button>
    </aside>
  );
}
